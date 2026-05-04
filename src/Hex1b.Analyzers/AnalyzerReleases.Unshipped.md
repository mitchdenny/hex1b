### New Rules

Rule ID    | Category         | Severity | Notes
-----------|------------------|----------|------
HEX1B0001  | Hex1b.ApiDesign  | Warning  | Widget extension or instance method name must not start with 'With' (reserved for `Hex1bTerminalBuilder`).
HEX1B0002  | Hex1b.ApiDesign  | Warning  | Type derived from `Hex1bWidget` must have a name ending in 'Widget'.
HEX1B0003  | Hex1b.ApiDesign  | Warning  | Type derived from `Hex1bNode` must have a name ending in 'Node'.
HEX1B0004  | Hex1b.ApiDesign  | Warning  | Type derived from `Hex1bWidget` must be declared as `record`.
HEX1B0005  | Hex1b.ApiDesign  | Warning  | Type derived from `Hex1bNode` must be declared as `class` (not `record`).
