# Vendored xterm.js KGP validation build

These browser assets are a matched production build from
[xterm.js PR #6098](https://github.com/xtermjs/xterm.js/pull/6098).

- Branch: `mitchdenny-fix-kitty-placements`
- Commit: `5b65c03690770673f407931c767f72ec908dce2c`
- Packages: `@xterm/xterm@6.0.0`, `@xterm/addon-fit@0.11.0`,
  `@xterm/addon-image@0.9.0`
- License: MIT (`xterm.LICENSE`)

| File | SHA-256 |
| --- | --- |
| `xterm.js` | `ce4cc36d5e30951cca5c6849de5a1465ab9a30fb5504cbe84a9a26579cc5d591` |
| `xterm.css` | `4d9a1d50808997f097ccc6040a5da6f6cb06b14e5ee2402df5196a218bba838f` |
| `xterm-addon-fit.js` | `acc70dbdb41ff8e6cb8b37ad888dabd2285267e97f79f2e72ab864fd6337df1c` |
| `xterm-addon-image.js` | `94fb5ca7413520807bb1efd9639f06c869ad8e913620393d4d7a02dba2ac5093` |

The full set is vendored so the demo does not combine the PR build with
different core or addon versions from a CDN. Replace these files with the
upstream npm packages after the changes are released.
