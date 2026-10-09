# The page design guide for an AI

`docs/AI_Page_Design_Guide.md` is the file to attach to an AI chat when it should make or convert pages of the HMI
(a Manual Movement or Machine Parameters page, another HMI's page turned into ours): which of our controls is for which
function, every control's properties (in the Properties window's order and categories, with their types, defaults and
what they are), their sizes and a real example of each from our pages, the values of the enums, the page layout,
addresses, scaling, translations, security, and what the AI should hand back - with one of our pages whole.

It is MADE FROM THE PROGRAM: `MakeAiPageGuide.cs` reads the built controls (`Kaban.UI.Design.PropertyOrder`, the
properties' attributes and the source's summaries) and the machines' designer files, and fills `guide_template.md`
(the hand-written sections). Make it again after a control or the pages change:

    tools/ai_guide/make_guide.sh Kaban.Machines.FA2010/bin/Debug

(It replaced HMI Settings' "AI Prompt" page-designer prompt, 9 October.)
