# Plans

## Indended features

- Includes (with sections and automatic guards)
- Functions
  - Inlines (with label renaming)
  - Automatically save registers (generic, don't enforce s0-sX)
  - Function calls with auto setting of a0-aX registers
  - Register naming (with variable definition)
  - [MAYBE] Auto name-register mapping
  - [MAYBE] Ignorable static type checking for arguments and returns
- Statistics (LOC, blanks, comment-only)
- Symbol poisoning
- Replacement macros

## Passes

- Include pass (include all files into a temporary one for processing)
- Symbol search (build lists with all functions, macros and instructions to poison)
- Function code read (read code for function implementations)
- Separate file implementations (implement all code into separate section files, applying macros, function inlines/implementations, regnames, etc.)
- Final file merge (merge all section files to final output file, stripping comments/blanks as needed)

## Directives

Copied/updated from old version:

- `include <path>`
- `sect <section>`
  - `section` MUST be one of header/data/mid/text/footer
- `funcdecl <name> [regsavehint] [inlinehint]`
  - `regsavehint` MUST be one of save/nosave
  - `inlinehint` MUST be one of aggressiveinline/noinline/autoinline
- `endfunc`
- `funccall <name> ???`
  - TODO: Decide on how to determine what regs to save on caller side

New directives:

- `poison <symbol>` (throw error if symbol found)
- `namereg <reg> <name>` (register renaming inside functions)
  - `reg` MUST be a RV32I register
- `imacro <name> <value>` (inline macro, for symbol replacement)
- `macro <name> [args]` (start multiline macro, for expressions)
  - If present, `args` must be separated by semicolon
- `endmacro` (end multiline macro)