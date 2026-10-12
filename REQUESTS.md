# Requests for Claude

Write here what is missing or wrong, whenever you notice it - one line or a paragraph each, in any order.
Claude reads this file at the start of every batch, does the items, and moves each one to "Done" with its commit.

## To do
"Bug reports without the conditions. “Axis monitor second page doesn’t scroll to the right end” took me a while to reproduce. It only happened at 150% scale, signed in as Admin, with Motion monitor columns ticked."
This bug doesn't seem to be solved properly, the scroll position still gets broken when there is too many control on the page and screen scale is big (the horizontal scroll movement gets big then, it could be the reason)
  - Claude (12 October): not reproduced yet - in the side panel and in its own window, at 150%, as Admin with every
    PLC axis item and all 43 Motion monitor columns (rows 8262 px wide), the scroll reaches the last column here.
    Please put a screenshot of the broken state in _requests (scrolled as far right as it goes) and write what you
    do: drag the scroll bar, click its arrows, the mouse wheel? Does it stop short, or jump back while you scroll?

## Done
- 931ba43 - The description texts' bottom was cut off (Machine Parameters > Others at 80%, captions on the left):
  a caption is now as high as its letters at every screen scale (_requests\ScreenScale_80%.bmp).
