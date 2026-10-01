import assert from "node:assert/strict";
import { test } from "node:test";
import { defaultLightPalette, defaultDarkPalette } from "../.build/terminal-palette.js";

function linearRgb(hex) {
  return hex.slice(1).match(/../gu).map(component => parseInt(component, 16) / 255)
    .map(value => value <= .04045 ? value / 12.92 : ((value + .055) / 1.055) ** 2.4);
}

function luminance(hex) {
  const [r, g, b] = linearRgb(hex);
  return .2126 * r + .7152 * g + .0722 * b;
}

function contrast(first, second) {
  const [a, b] = [luminance(first), luminance(second)];
  return (Math.max(a, b) + .05) / (Math.min(a, b) + .05);
}

function hue(hex) {
  const [r, g, b] = linearRgb(hex);
  const l = Math.cbrt(.4122214708 * r + .5363325363 * g + .0514459929 * b);
  const m = Math.cbrt(.2119034982 * r + .6806995451 * g + .1073969566 * b);
  const s = Math.cbrt(.0883024619 * r + .2817188376 * g + .6299787005 * b);
  const a = 1.9779984951 * l - 2.428592205 * m + .4505937099 * s;
  const B = .0259040371 * l + .7827717662 * m - .808675766 * s;
  return Math.atan2(B, a) * 180 / Math.PI;
}

test("Hex1b defaults exchange foreground/background without using stark black or white", () => {
  assert.equal(defaultDarkPalette.background, "#323232");
  assert.equal(defaultDarkPalette.ansi[0], "#242424");
  assert.equal(defaultDarkPalette.foreground, "#d4d0c8");
  assert.equal(defaultLightPalette.background, defaultDarkPalette.foreground);
  assert.equal(defaultLightPalette.foreground, defaultDarkPalette.background);
  for (const palette of [defaultDarkPalette, defaultLightPalette]) {
    assert.ok(Object.isFrozen(palette) && Object.isFrozen(palette.ansi));
    assert.equal(palette.ansi.length, 16);
    assert.equal(new Set(palette.ansi).size, 16);
    const body = contrast(palette.foreground, palette.background);
    assert.ok(body >= 8 && body <= 8.5, `Body contrast: ${body}`);
    assert.ok(contrast(palette.ansi[8], palette.background) >= 4.5, "Readable bright-black text");
  }
});

test("Normal and bright chromatic slots keep bounded, readable contrast in both modes", () => {
  for (const palette of [defaultDarkPalette, defaultLightPalette]) {
    const normalTarget = palette === defaultDarkPalette ? 5.2 : 4.6;
    const brightTarget = palette === defaultDarkPalette ? 6.3 : 5.2;
    for (let index = 1; index <= 6; index++) {
      const normal = contrast(palette.ansi[index], palette.background);
      const bright = contrast(palette.ansi[index + 8], palette.background);
      assert.ok(Math.abs(normal - normalTarget) <= .1, `ANSI ${index}: ${normal}`);
      assert.ok(Math.abs(bright - brightTarget) <= .1, `ANSI ${index + 8}: ${bright}`);
      assert.ok(normal >= 4.5 && bright >= 4.5);
      assert.ok(bright > normal);
      assert.ok(bright < contrast(palette.foreground, palette.background));
    }
  }
});

test("Mode and intensity changes preserve the hue identity of each named chromatic slot", () => {
  for (let index = 1; index <= 6; index++) {
    const colors = [defaultDarkPalette.ansi[index], defaultDarkPalette.ansi[index + 8],
      defaultLightPalette.ansi[index], defaultLightPalette.ansi[index + 8]];
    for (const color of colors) {
      const difference = Math.abs(hue(color) - hue(colors[0]));
      assert.ok(Math.min(difference, 360 - difference) < 3, `ANSI ${index}: hue drift for ${color}`);
    }
  }
});
