using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The full back-and-forth dialogue for one NPC, edited as a component on the NPC GameObject and read by <see cref="NpcController"/>.
/// Each <see cref="DialogueStep"/> carries its own <see cref="DialogueStep.speakerName"/> ("player", "npc", ...), so a single list
/// naturally holds both speakers' lines in the spoken order. Attach one Conversation per NPC; the lines are whatever you type in
/// the Inspector, so whole conversations are authored in the Unity Editor, not in code.
///
/// If the Inspector list is empty (common the first time you add the component before typing anything), <see cref="GetSteps"/>
/// rebuilds the placeholder conversation at runtime so the NPC still talks — but prefer filling the list in the Inspector.
/// </summary>
[RequireComponent(typeof(NpcController))]
public class Conversation : MonoBehaviour
{
    [Tooltip("The NPC and player's lines, in speaking order. Each line's speakerName says who speaks it.")]
    [TextArea(2, 4)]
    [SerializeField] private List<DialogueStep> steps = new List<DialogueStep>();

    /// <summary>The steps to play. Falls back to a placeholder conversation if the Inspector list is empty.</summary>
    public List<DialogueStep> GetSteps()
    {
        if (steps != null && steps.Count > 0)
        {
            return steps;
        }
        return DefaultSteps();
    }

    // Two of these steps drive the wider game: Writing hands off to the hanzi tracing scene, GoTop points the player upstairs.
    // Names here are whatever the NPC/Player are called; swap them in the Inspector per NPC.
    //
    // FIRST CONVERSATION — the Soldier (李沅诺) who explains where you are and where to find 过品玉.
    // Structure: greeting -> translation pop quiz (branch) -> name reveal with 过 (Guò) family reveal -> directions to 王喜悦, with three characters to trace to unlock the next area.
    private static List<DialogueStep> DefaultSteps()
    {
        return new List<DialogueStep>
        {
            // — greeting —
            new DialogueStep { speakerName = "soldier", text = "你好！你来这里做什么？ (Nǐ hǎo! Nǐ lái zhèlǐ zuò shénme?)", action = DialogueAction.None },
            new DialogueStep { speakerName = "player", text = "你好! (Nǐ hǎo!)", action = DialogueAction.None },

            // — translation pop quiz: three choices; only choice [1] is correct and advances the name-gated path —
            new DialogueStep
            {
                speakerName = "soldier",
                text = "你叫什么名字？(Nǐ jiào shénme míngzì?)",
                action = DialogueAction.None,
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice { label = "What is your name?",   targetStep = 3, action = DialogueAction.None },
                    new DialogueChoice { label = "How are you?",         targetStep = 7, action = DialogueAction.None },
                    new DialogueChoice { label = "Where are you from?", targetStep = 8, action = DialogueAction.None },
                }
            },
            new DialogueStep { speakerName = "player", text = "我叫过小月。(Wǒ jiào Guòxiǎoyuè.)", action = DialogueAction.None },
            new DialogueStep { speakerName = "soldier", text = "你好，小月 (xiǎo yuè)。我叫 李沅诺 (Lǐ yuán nuò)。等等，你说你的姓是 过 (Guò)?", action = DialogueAction.None },
            // — family-name reveal; [1] leads to magistrate reveal, [2] ends the conversation early —
            new DialogueStep
            {
                speakerName = "soldier", text = "你和 过品玉 (Guò pǐn yù) 有关系吗？",
                action = DialogueAction.None,
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice { label = "是的！我是她的后代。你怎么知道？(Yes! I am. How did you know?)", targetStep = 6, action = DialogueAction.None },
                    new DialogueChoice { label = "不是，你搞错人了。(No, you must be mistaken.)",                    targetStep = 9, action = DialogueAction.None },
                }
            },
            new DialogueStep { speakerName = "soldier", text = "她是我们本地的官员，为我们制作最好的器物。没人知道怎么做的，但也没人问。(She is our local magistrate. She makes the best equipment.)", action = DialogueAction.None },
            // — misunderstanding branches both eventually re-ask the name, then re-present the quiz when chosen again —
            new DialogueStep { speakerName = "soldier", text = "哦！'How are you?' 应该是 '你好吗 (nǐ hǎo ma)'。祝你一切顺利。不好意思，你的名字是？", action = DialogueAction.None },
            new DialogueStep { speakerName = "soldier", text = "哦！'Where are you from?' 应该是 '你从哪里来 (nǐ cóng nǎlǐ lái)'。不好意思，你的名字是？", action = DialogueAction.None },
            // — post-reveal: where to find 过品玉 — the reply is gated by a Writing action that resumes the conversation once the player traces the characters.
            new DialogueStep
            {
                speakerName = "player", text = "你知道我在哪里可以找到她吗？",
                action = DialogueAction.None,
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice { label = "是的，请告诉我 (Yes, please tell me).",  targetStep = 11, action = DialogueAction.None },
                    new DialogueChoice { label = "我再看看 (I'll look around first).",         targetStep = -1, action = DialogueAction.None },
                }
            },
            new DialogueStep { speakerName = "soldier", text = "她从来不在同一个地方停留 (tā cóng bù zài tóng yī gè dìfāng tíngliú)，不过王喜悦 (Wáng xǐyuè) 应该知道她在哪。她在镇上卖丝绸，你可以去问她。", action = DialogueAction.None },
            // — trace gate: player traces 问 / 要 / 的 then the gate unlocks and the walk-to directions resume at step 13 —
            new DialogueStep
            {
                speakerName = "soldier", text = "感谢你！(Xièxiè) 现在临摹 问, 要, 的 这三个字 (xiě zhè sān gè zì) 来解锁下一个区域。别惹麻烦 (bié rě máfán)。",
                action = DialogueAction.Writing,
                choices = new List<DialogueChoice>
                {
                    new DialogueChoice { label = "我开始临摹吧 (I'm ready to trace).", targetStep = -1, action = DialogueAction.None },
                }
            },
            new DialogueStep { speakerName = "soldier", text = "很好！新区域解锁，一直往前走去找 王喜悦 (Wáng xǐyuè)。", action = DialogueAction.GoTop },
            new DialogueStep { speakerName = "player", text = "谢谢！我这就去找她。(Xièxiè! Wǒ zhè jiù qù zhǎo tā.)", action = DialogueAction.None },
            new DialogueStep { speakerName = "soldier", text = "快走，她不会在一个地方停留太久！(Go quickly, she never stays long!)", action = DialogueAction.None },
        };
    }

    // Helper so the Soldier (and future NPCs) can declare an "end conversation" target step in a choice without spreading index arithmetic through the data.
    private const int EndConversationStep = -1;
}
