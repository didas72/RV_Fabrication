# Passes

1) Include pass (include all files into a temporary one for processing)
2) Macro search (build list with all macros)
3) Macro application (replace all macros on new file)
4) Symbol search (build lists with all functions and poisoned symbols)
5) Section implementation (implement all code into separate section files, function inlines/implementations, check poisoned symbols, read sectord, etc.)
6) Final file merge (merge all section files to final output file, stripping comments/blanks as needed)
