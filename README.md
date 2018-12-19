# Pic2SciFont

Very simple deal, you give it an image file (PNG preferred) with a particular layout and it'll give you a Sierra SCI Font resource. If you simply drag and drop the file onto the application icon, it'll assume you want the same filename for the output but with ".fon" at the end instead. The first character cell found is used to set the font's line height and does not itself appear in the result. Duplicates (for example those little single-dot placeholders) are optimized away.

A cell is any rectangular area that's *only* pure black or white fully surrounded by anything else.

# Voc999Creator

Even simpler deal. You run it, it produces a `999.voc` resource, a list of kernel call names. If you provide a file that's simply one bare name on each line (by dropping it like with Pic2SciFont, for example), it'll use that instead of a hardcoded list.

# Voc997Creator

If there's a `997.txt` and no `997.voc` file, or the former is more recently changed than the latter, this one'll create a `997.voc` resource, a list of selectors, based on the text file. The exact opposite also holds. If you pass a file name, it'll turn a `.txt` file into a similarly-named `.voc` file and vice versa. The interesting bit is that you can use `//<num>` comment lines to skip ahead, which is quite a space saver when you consider `-objID-` is #4096.