## Views (SCI11)

### View

Type | Name | Notes
-----|------|-------
u16  | view header size | skip ahead this far to get to loop 1
u8   | # of loops
u8   | view flags | bit 6 set if it's uncompressed
u8   | high split flag | unused
u8   | (dummy)
u16  | # of cels
u32  | palette offset | null if none
u8   | loop header size
u8   | cel header size
u32  | animation offset | unused

Loops follow directly after, in order.

### Loop

Type | Name | Notes
-----|------|-------
u8   | alternate loop | for mirroring
u8   | flags | for mirroring
u8   | # of cels
u8   | (dummy)
u8   | starting cel | unused
u8   | ending cel | unused
u8   | repeat count | unused
u8   | step size | unused
u32  | palette offset | unused
u32  | offsets to cels for this loop

### Cel

Type | Name | Notes
-----|------|-------
u16  | width
u16  | height
s16  | X offset
s16  | Y offset
u8   | skip color  | transparent
u8   | compression
u16  | compression remap count | unused
u32  | compressed size | only used in SEQ
u32  | control size | only used in SEQ
u32  | palette offset | unused
u32  | data offset
u32  | color offset
u32  | compression remap offset | unused

## Pictures (SCI11)

Type | Name | Notes
-----|------|------
u16  | header size
u8   | # of priorities | unused
u8   | # of priority lines | should be 16
u8   | # of cels | but really bool `hasCel`
u8   | (dummy)
u16  | vanishing point X | unused
u16  | vanishing point Y | unused
u16  | viewing angle | unused
u32  | vector data size | unused
u32  | vector data offset
u32  | priority cel offset | unused in SCI11
u32  | control cel offset | unused in SCI11
u32  | palette offset
u32  | visual header offset
u32  | polygon offset
u16  | priority lines

## Fonts

### Font

Type   | Name | Notes
-------|------|-------
u16    | lowest char
u16    | highest char
u16    | point size
u16... | character cells

"Lowest char" is *not* the index of the first character in the font. It's the lowest character index, inclusive, that the interpreter will agree to draw. It is practically always 0.

### Character

Type  | Name | Notes
------|------|-------
u8    | width
u8    | height

1bpp pixel data follows directly after.
