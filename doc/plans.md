# Plans

## Indended features

- [DONE] Includes (with sections and automatic guards)
- [DONE] Functions
  - [DONE] Inlines (with label renaming)
  - [DONE] Automatically save registers (generic, don't enforce s0-sX)
  - [DONE] Function calls with auto setting of a0-aX registers
  - [DONE (with imacros)] Register naming (with variable definition)
  - [MAYBE] Auto name-register mapping
  - [MAYBE] Ignorable static type checking for arguments and returns
- [DONE] Statistics (LOC, blanks, comment-only, directives)
- [DONE] Symbol poisoning
- [DONE] Replacement macros

## Passes

- Include pass (include all files into a temporary one for processing)
- Macro search (build list with all macros)
- Macro application (replace all macros on new file)
- Symbol search (build lists with all functions and poisoned symbols)
- Section implementation (implement all code into separate section files, function inlines/implementations, check poisoned symbols, read sectord, etc.)
- Final file merge (merge all section files to final output file, stripping comments/blanks as needed)

## Directives

Copied/updated from old version:

- `include <path>`
- `sect <section>`
- `funcdecl <name> <argcount> [inlinehint]` [cant be nested neither in macros nor functions]
  - `inlinehint` MUST be one of aggressiveinline/noinline/autoinline, defaults to autoinline
- `endfunc` [cant be nested neither in macros nor functions]
- `funccall <name> [args]* [save <saveregs>*]`
  - `args` MUST be RV32I registers other than `sp` and `ra` or `_` for already set
  - `saveregs` MUST be RV32I registers other than `sp` and `ra`

New directives:

- `sectord [sects]*` (define section ordering, unspecified sections will be added at the end in no particular order) [only one MAY be in the provided source files]
- `poison <symbol>*` (throw error if symbol found)
- `imacro <name> <value>` (inline macro, for symbol replacement)
- `macro <name> [args]` (start multiline macro, for expressions) [cant be nested neither in macros nor functions]
  - If present, `args` must be separated by semicolon
- `endmacro` (end multiline macro)