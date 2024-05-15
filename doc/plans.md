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
- Macro search (build list with all macros)
- Macro application (replace all macros on new file)
- Function search (build lists with all functions and poisoned symbols)
- Code read (read code for functions and check poisoned symbols)
- Section implementation (implement all code into separate section files, function inlines/implementationss, etc.)
- Final file merge (merge all section files to final output file, stripping comments/blanks as needed)

## Directives

Copied/updated from old version:

- `include <path>`
- `sect <section>`
  - `section` MUST be one of header/data/mid/text/footer
- `funcdecl <name> [regsavehint] [inlinehint]` [cant be nested neither in macros nor functions]
  - `regsavehint` MUST be one of save/nosave, defaults to save
  - `inlinehint` MUST be one of aggressiveinline/noinline/autoinline, defaults to autoinline
- `endfunc` [cant be nested neither in macros nor functions]
- `funccall <name> [args]* [; <saveregs>*]`
  - `args` MUST be RV32I registers other than `sp` and `ra`
  - `saveregs` MUST be RV32I registers other than `sp` and `ra`

New directives:

- `poison <symbol>*` (throw error if symbol found)
- `imacro <name> <value>` (inline macro, for symbol replacement)
- `macro <name> [args]` (start multiline macro, for expressions) [cant be nested neither in macros nor functions]
  - If present, `args` must be separated by semicolon
- `endmacro` (end multiline macro)