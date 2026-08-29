# Vendored xterm.js KGP validation build

These browser assets are a matched production build from
[xterm.js PR #6098](https://github.com/xtermjs/xterm.js/pull/6098).

- Branch: `mitchdenny-fix-kitty-placements`
- Commit: `c34b04b5bba64609974110838c5a5f498ff16468`
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
| `xterm-addon-image.js` | `e8d11aae3526cdfe48afd53b839008a5cf132854b7ac37cad6f17fbb3105fc6e` |
| `xterm.LICENSE` | `b569f629d00f2626a8100df2a1798210535621e42164dfd426a6fe5aac7b0ccd` |
| `LICENSE.addon-fit.txt` | `e256f01188af527e4d06d21d06fbf785ae9c50d4b328bf03cbe0ba7f0aa4228f` |
| `LICENSE.addon-image.txt` | `00994f891153ba5d613e2b838a9ad2f47d4484070cb594b46efc547ebea5ca05` |
| `addon-image.js.LICENSE.txt` | `0d5cae8c21a98d6eb5aa651d1ba2f41ba270a1caffa78645e5c25dee80370729` |

The full set is vendored so the demo does not combine the PR build with
different core or addon versions from a CDN. Replace these files with the
upstream npm packages after the changes are released.

This build includes the overwritten-cell reconciliation used by visible
placement deletion. The production bundle retains the
`reconcileImageCellIndexes` marker so the regression test can prove that the
served addon contains the tombstone fix rather than the earlier PR build.
