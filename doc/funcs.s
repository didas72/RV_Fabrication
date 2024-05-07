# Note: interpret frame pointer as s0
# #;funcdecl <name> autosave/handsave forceinline/noinline leaf/noleaf <savec> <tregc>
#
# Before function code
#   If func is autosave:
#    - Push registers s0-sX
#
# After function code, before ret
#   If func is autosave:
#    - Pop registers s0-sX

#;funcdecl fancyop autosave noinline leaf 0 2
#fancyop => q = (a+b)/c && r = (a+b)%c
fancyop:
	add t0, a0, a1
	div a0, t1, a2
	rem a1, t1, a2
	ret
#;endfunc

# Note: report original location when funccall
# #;funccall <name> <tregs>
#
# Before function call:
#   If func is noinline and leaf:
#    - Pushes tX where X < min(tregs, func.tregc)
#    - Pushes ra
#   If func if noinline and noleaf:
#    - Pushes tX where X < tregs
#    - Pushes ra
#   If func is forceinline and leaf:
#    - Pushes tX where X < min(tregs, func.tregc)
#    - Includes func code (and it's saves)
#   If func is forceinline and noleaf:
#    - Pushes tX where X < tregs
#    - Includes func code (and it's saves) and inlines recursively
#
# After function return:
#   If func is noinline and leaf:
#    - Pops ra
#    - Pops tX where X < min(tregs, func.tregc)
#   If func is noinline and noleaf:
#    - Pops ra
#    - Pops tX where X < tregs
#   If func is forceinline and noleaf:
#    - Pops tX where X < min(tregs, func.tregc)
#   If func is forceinline and leaf:
#    - Pops tX where X < tregs

#;funccall fancyop 0
