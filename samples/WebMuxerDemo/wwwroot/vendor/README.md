# Vendored xterm.js KGP validation build

These browser assets are a matched production build from
[xterm.js PR #6098](https://github.com/xtermjs/xterm.js/pull/6098).

- Branch: `mitchdenny-fix-kitty-placements`
- Commit: `825f5605c9afc14cc1f7579959a9699c23df3f0f`
- Packages: `@xterm/xterm@6.0.0`, `@xterm/addon-fit@0.11.0`,
  `@xterm/addon-image@0.9.0`
- License: MIT (`xterm.LICENSE`, `LICENSE.addon-fit.txt`, and
  `LICENSE.addon-image.txt`); generated package notices are preserved in
  `addon-image.js.LICENSE.txt`

| File | SHA-256 |
| --- | --- |
| `xterm.js` | `ce4cc36d5e30951cca5c6849de5a1465ab9a30fb5504cbe84a9a26579cc5d591` |
| `xterm.css` | `4d9a1d50808997f097ccc6040a5da6f6cb06b14e5ee2402df5196a218bba838f` |
| `xterm-addon-fit.js` | `acc70dbdb41ff8e6cb8b37ad888dabd2285267e97f79f2e72ab864fd6337df1c` |
| `xterm-addon-image.js` | `bfcbf94862d6909cbbb3bb0e0fd0ec821c88317a5c7acac9bc1c67209d2dafe3` |
| `xterm.LICENSE` | `b569f629d00f2626a8100df2a1798210535621e42164dfd426a6fe5aac7b0ccd` |
| `LICENSE.addon-fit.txt` | `e256f01188af527e4d06d21d06fbf785ae9c50d4b328bf03cbe0ba7f0aa4228f` |
| `LICENSE.addon-image.txt` | `00994f891153ba5d613e2b838a9ad2f47d4484070cb594b46efc547ebea5ca05` |
| `addon-image.js.LICENSE.txt` | `0d5cae8c21a98d6eb5aa651d1ba2f41ba270a1caffa78645e5c25dee80370729` |

The full set is vendored so the demo does not combine the PR build with
different core or addon versions from a CDN. Replace these files with the
upstream npm packages after the changes are released.

This build rebuilds targeted image-cell indexes from attached normal and
alternate buffer lines before deletion. That lets named placement replacement
clear live cells even when terminal rendering replaced the original line
objects. Alongside the artifact hash, the regression pins the production
rebuild-before-clear sequence
`this._rebuildImageCellIndex(A);const s=this._clearImageCells(A)` to
distinguish the live-line tombstone fix from earlier PR builds.
