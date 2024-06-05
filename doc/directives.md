# RV_Fabrication directives

Directives for RV_Fabrication respect the following format:  
`;<directive> [args]*`

## Directives

Currently, the available directives are:

Directive | Description | Notes
-- | -- | --
`include <path>` | Includes another source code file, respecting section order. | `path` is interpreted as being a relative path from the current file's location.
`sect <section>` | Starts a new section. Code between this and the next `sect` directive will be put inside the `section` section. | -
`funcdecl <name> <argcount> [inlinehint]` | Declares a new function `name` which takes `argcount` arguments. MAY be given an `inlinehint`. | `inlinehint` MUST be one of `autoinline`, `aggressiveinline` or `noinline`. Defaults to `autoinline`.
`endfunc` | Ends a function declaration, setting the end of the function's code. There MUST be exactly one `endfunc` per `funcdecl`.  | -
`funccall <name> [args]* [save <saveregs>*]` | Calls function `name` with the provided `args`, optionally saving `saveregs` registers. |  Both `args` and `saveregs` MUST be RV32I registers other than `sp` and `ra`.  `args` MUST have exactly the same `argcount` registers as the called function.  The keyword `save` MAY be used without the succeeding `saveregs`.
`sectord [sects]*` | Defines the order in which sections are to be included in the output file. | Unspecified sections will be added at the end in no particular order. Only one `sectord` CAN be present in all the included files.
`poison <symbols>*` | Poisons one or more `symbols`, resulting in an error if any of the symbols is found in the code. Comments are not checked for poisoned symbols. | Poisoned symbols are checked across all source lines, included the code above the symbol poisoning.
`imacro <name> <value>` | Declares an inline macro. Inline macros are single-line symbol replacements without arguments. | `imacro`s are processed after includes but before the other directives. IMacro expansion is NOT recursive.
`macro <name> [args]` | Declares a macro. Macros are multi-line, inline-function like structures that are replaced in dedicated lines. | `macro`s are processed after includes but before the other directives. Macro expansion is NOT recursive.
`endmacro` | Ends a macro declaration, setting the end of the macro's code. There MUST be exactly one `endmacro` per `macro`. | -

Directives also obey the following additional rules:

- Only `funccall` may be used inside function declarations.
