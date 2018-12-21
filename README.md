# Pic2SciFont

Very simple deal, you give it an image file (PNG preferred) with a particular layout and it'll give you a Sierra SCI Font resource. If you simply drag and drop the file onto the application icon, it'll assume you want the same filename for the output but with ".fon" at the end instead. The first character cell found is used to set the font's line height and does not itself appear in the result. Duplicates (for example those little single-dot placeholders) are optimized away.

A cell is any rectangular area that's *only* pure black or white fully surrounded by anything else.

You can also give it a Sierra FON file and get a PNG in the correct format.

# Voc999Creator

Even simpler deal. You run it, it produces a `999.voc` resource, a list of kernel call names. If you provide a file that's simply one bare name on each line (by dropping it like with Pic2SciFont, for example), it'll use that instead of a hardcoded list.

# Voc997Creator

If there's a `997.txt` and no `997.voc` file, or the former is more recently changed than the latter, this one'll create a `997.voc` resource, a list of selectors, based on the text file. The exact opposite also holds. If you pass a file name, it'll turn a `.txt` file into a similarly-named `.voc` file and vice versa. The interesting bit is that you can use `//<num>` comment lines to skip ahead, which is quite a space saver when you consider `-objID-` is #4096.

# Utf8Message

Given a message resource file in text format as exported by SV, this'll compile it back into a valid resource file. But there's a twist: if the first line is `!utf8`, the text will be read and stored as such. It also accepts `//` comments and blank lines. Blank lines that are *part of a message* start with five tabs and won't be skipped.
`AutoSave\05 Interval` can thus be rewritten as `AutoSave™ Interval`, where the trademark is stored as-is as a three-byte sequence instead of a three-byte escape. UTF-8 message resources have a little marker at the end, ignored by the interpreter.

Again, you can also provide a message resource file, and get a properly-formatted text file back, including the `!utf8` line if the input calls for it.

If there are `.sh` files available, these will be used to clarify the noun, verb, condition, and talker values. Talker values are taken from `talkers.sh`, verbs from `verbs.sh`, nouns and conditions from `<basename>.sh`. This uses a very simple parser that works on SCI Companion's files so don't muck them up.
