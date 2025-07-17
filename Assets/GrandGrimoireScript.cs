using KModkit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class GrandGrimoireScript : MonoBehaviour
{
    static int _moduleIdCounter = 1;
    int _moduleID = 0;

    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMSelectable[] Buttons;
    public Sprite[] GemSprites;
    public Sprite[] Symbols;
    public Sprite[] Papers;
    public SpriteRenderer[] Dots;
    public SpriteRenderer Gem;
    public SpriteRenderer Blood;
    public TextMesh[] Texts;
    public MeshRenderer StatusLight;

    private Coroutine StrikeAnim;
    private SpriteRenderer Glow;
    private List<SpriteRenderer> ButtonSprites = new List<SpriteRenderer>();
    private List<int> BookContents = new List<int> { 0, 1, 2, 3, 4, 5, 6 };
    private List<int> Actions = new List<int>();
    private List<int> Priority = new List<int>() { 0, 1, 2, 3, 7, 4, 5, 6 };
    private static readonly int[][] Table = new int[][] {
        new int[]{ -1, 3, 1, 4, 2, 6, 5 },
        new int[]{ 2, -1, 5, 0, 6, 4, 3 },
        new int[]{ 6, 0, -1, 1, 5, 3, 4 },
        new int[]{ 4, 5, 6, -1, 0, 2, 1 },
        new int[]{ 5, 6, 3, 2, -1, 1, 0 },
        new int[]{ 1, 4, 0, 6, 3, -1, 2 },
        new int[]{ 3, 2, 4, 5, 1, 0, -1 } };
    private int Addend, Completed, Page;
    private bool[] Bloodied = new bool[7];
    private bool[] Cast = new bool[7];
    private bool Bezella, Solved;

    private Settings _Settings;
    class Settings
    {
        //public bool RemoveSpoilers = false;
        public bool RemoveBlood = false;
    }
    void GetSettings()
    {
        var SettingsConfig = new ModConfig<Settings>("GrandGrimoire");
        _Settings = SettingsConfig.Settings; // This reads the settings from the file, or creates a new file if it does not exist
        SettingsConfig.Settings = _Settings; // This writes any updates or fixes if there's an issue with the file
    }

    private static readonly string[][] SpellInfo = new string[][] {
        //Spoilered
        new string[] { "Ignaize", "[Inferno Spell]", "Summons a circle of flame within\na 1 metre radius of the caster.\nRequires an incantation and\nsceptre to cast." },
        new string[] { "Dimere", "[Vanishing Spell]", "Causes anything the caster touches\nto vanish from sight. Simply chant\n\"Amere\" to make it reappear." },
        new string[] { "Goldor", "[Transmutation Spell]", "Transmutes the caster's target into\nsolid gold. The spell will transmute\nthe closest target within range." },
        new string[] { "Famalia", "[Shadow Familiar Spell]", "Summons forth a magical familiar\nfrom the shadows. Although it\npossesses a physical form, it is\ncompletely weightless." },
        new string[] { "Godoor", "[Portal Spell]", "Creates a portal on two sides of\ngreen-coloured walls. The portal\nwill disappear after five minutes." },
        new string[] { "Fainfol", "[Instant Sleep Spell]", "Causes those who hear the\nincantation to lose all\nconsciousness. The effect takes\nhold the second the incantation is\nheard." },
        new string[] { "Granwyrm", "[The Great Fire Dragon]", "Summons forth a great fire dragon\nfor the caster to command.\nResponsible for the \"Legendary\nFire\" that lit the town ablaze so\nmany years ago." },
        //Un-spoilered
        new string[] { "Flarim", "[Fire Spell]", "Summons a pillar of flame to\nconsume a target. The target\ncannot be more than 2 metres\naway from the caster." },
        new string[] { "Gobeye", "[Invisibility Spell]", "Causes the caster and their\npossessions to become invisible.\nChant this spell again to reappear." },
        new string[] { "Sapphusa", "[Transmutation Spell]", "Transmutes the caster's target into\nsolid sapphire. This effect is\npermanent." },
        new string[] { "Lugisal", "[Ghost Spell]", "Summons forth an apparition,\nwhich will obey commands. The\nghost will have no weight, but can\nstill interact with objects." },
        new string[] { "Halnu", "[Portal Spell]", "Creates two circular portals on\ntwo smooth, flat walls. Their\nmaximum diameters are the\nheight of the caster." },
        new string[] { "Fleefott", "[Sleeping Spell]", "Causes those within an\nunobstructed 5 metre radius of the\ncaster to instantly lose\nconsciousness." },
        new string[] { "Dakenda", "[Phoenix Spell]", "Summons forth a phoenix for the\ncaster to command. This phoenix\nhas the ability to spit fire." }
    };

    private static readonly string[] SpellDescriptionsNormalised = new string[]
    {
        "summons a circle of flame within a one metre radius of the caster requires an incantation and sceptre to cast",
        "causes anything the caster touches to vanish from sight simply chant amere to make it reappear",
        "transmutes the casters target into solid gold the spell will transmute the closest target within range",
        "summons forth a magical familiar from the shadows although it possesses a physical form it is completely weightless",
        "creates a portal on two sides of green coloured walls the portal will disappear after five minutes",
        "causes those who hear the incantation to lose all consciousness the effect takes hold the second the incantation is heard",
        "summons forth a great fire dragon for the caster to command responsible for the legendary fire that lit the town ablaze so many years ago"
    };

    private int DiscardedPos(List<int> candidates)
    {
        switch (BookContents[6])
        {
            case 0:     //Ignaize
                if (Bomb.GetOnIndicators().Count() > Bomb.GetOffIndicators().Count())
                    return 0;
                else if (Bomb.GetOffIndicators().Count() > Bomb.GetOnIndicators().Count())
                    return 1;
                else
                    return 2;
            case 1:     //Dimere
                return ((Bomb.GetPortCount() + 2) % 3);
            case 2:     //Goldor
                if (candidates.Contains(2))
                    return 0;
                else if (candidates.Contains(6))
                    return 1;
                else
                    return 2;
            case 3:     //Famalia
                return ((Bomb.GetBatteryCount() + 2) % 3);
            case 4:     //Godoor
                return ((Bomb.GetSerialNumberNumbers().Last() + 2) % 3);
            case 5:     //Fainfol
                return (Mathf.Min(Mathf.FloorToInt(BookContents.IndexOf(1) / 2), 2) + 1) % 3;
            default:    //Granwyrm
                if (Bomb.GetSerialNumberLetters().Any(x => "OATH".Contains(x)))
                    return 0;
                else if (Bomb.GetSerialNumberLetters().Any(x => "DEATHS".Contains(x)))
                    return 1;
                else
                    return 2;
        }
    }

    void Awake()
    {
        _moduleID = _moduleIdCounter++;
        GetSettings();
        //Addend = _Settings.RemoveSpoilers ? 7 : 0;
        Blood.color = new Color();
        Glow = StatusLight.transform.GetComponentInChildren<SpriteRenderer>();
        StatusLight.material.color = new Color();
        Glow.color = new Color();
        StartCoroutine(AnimStatus());
        BookContents.Shuffle();
        Debug.LogFormat("[The Grand Grimoire #{0}] The order of the spells is {1}.", _moduleID, BookContents.Select(x => SpellInfo[x + Addend][0]).Join(", "));
        UpdateContents();
        for (int i = 0; i < Buttons.Length; i++)
        {
            int x = i;
            ButtonSprites.Add(Buttons[x].GetComponent<SpriteRenderer>());
            Buttons[x].OnInteract += delegate { if (!Solved) ButtonPress(x); return false; };
            Buttons[x].OnHighlight += delegate { if (((x == 0 && Page > 0) || (x == 1 && Page < 6)) && !Solved) ButtonSprites[x].color = new Color32(255, 69, 0, 255); else if (x == 2 && !Solved) Buttons[2].GetComponentInChildren<TextMesh>().color = new Color32(255, 69, 0, 255); else if (x == 3 && !Solved) Buttons[3].GetComponentsInChildren<SpriteRenderer>()[1].color = new Color32(255, 69, 0, 255); };
            Buttons[x].OnHighlightEnded += delegate { if (((x == 0 && Page > 0) || (x == 1 && Page < 6)) && !Solved) ButtonSprites[x].color = new Color(0, 0, 0, 1); else if (x == 2 && !Solved) Buttons[2].GetComponentInChildren<TextMesh>().color = new Color(0, 0, 0, 1); else if (x == 3 && !Solved) Buttons[3].GetComponentsInChildren<SpriteRenderer>()[1].color = Bloodied[Page] && _Settings.RemoveBlood ? new Color(0, 0, 0, 0.25f) : new Color(0, 0, 0, 1); };
        }
        ButtonSprites[0].color = new Color();
    }

    // Use this for initialization
    void Start()
    {
        Calculate();
    }

    void Calculate()
    {
        Bezella = false;
        if (Bomb.GetSerialNumberNumbers().Where(x => x % 2 == 1).Count() == Bomb.GetSerialNumberNumbers().Count())
            Bezella = true;

        Debug.LogFormat("[The Grand Grimoire #{0}] You are {1}the Great Witch Bezella.", _moduleID, Bezella ? "" : "not ");
        if (!Bezella)
        {
            List<int> candidates = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                candidates.Add(Table[BookContents[i * 2]][BookContents[(i * 2) + 1]]);
                while (candidates.Where((x, index) => index < i).Contains(candidates[i]))
                    candidates[i] = BookContents[(BookContents.IndexOf(candidates[i]) + 1) % 7];
            }
            Debug.LogFormat("[The Grand Grimoire #{0}] The three candidates are {1}.", _moduleID, candidates.Select(x => SpellInfo[x + Addend][0]).Join(", "));
            var discarded = DiscardedPos(candidates);
            Debug.LogFormat("[The Grand Grimoire #{0}] The candidate that is not in your Talea Magica is {1}.", _moduleID, SpellInfo[candidates[discarded] + Addend][0]);
            candidates.RemoveAt(discarded);
            foreach (int i in new int[] { 1, 4, 5, 3, 0, 2, 6 })
                if (candidates.Contains(i))
                    Actions.Add(i);
            Actions.Insert(Actions.Where(x => new int[] { 1, 4, 5, 3 }.Contains(x)).Count(), 7);
            Debug.LogFormat("[The Grand Grimoire #{0}] Therefore, the actions to be performed are {1}.", _moduleID, Actions.Select(x => x == 7 ? "Dagger" : SpellInfo[x + Addend][0]).Join(", "));
        }
        else
        {
            var batteries = Bomb.GetBatteryCount() + 7;
            while (batteries > 7)
                batteries -= 7;
            batteries--;
            var description = SpellDescriptionsNormalised[BookContents[batteries]];
            Debug.LogFormat("[The Grand Grimoire #{0}] The number of batteries is {1}, so you must read {2}'s description.", _moduleID, Bomb.GetBatteryCount(), SpellInfo[BookContents[batteries]][0] + Addend);
            Debug.LogFormat("[The Grand Grimoire #{0}] Its description is \"{1}\".", _moduleID, SpellInfo[BookContents[batteries] + Addend][2].Replace('\n', ' '));

            var descriptionArray = description.Split(' ');

            var indicators = Bomb.GetIndicators().Count() % descriptionArray.Length;
            var word = descriptionArray[indicators];
            Debug.LogFormat("[The Grand Grimoire #{0}] The number of indicators is {1}, so you must use the word \"{2}\".", _moduleID, Bomb.GetIndicators().Count(), word);

            var ports = Bomb.GetPorts().Count() + word.Length;
            while (ports > word.Length)
                ports -= word.Length;
            ports--;

            var ix = 0;
            for (int i = 0; i < indicators; i++)
                ix += descriptionArray[i].Length;
            ix += ports;

            var descriptionDespaced = descriptionArray.Join("");
            Debug.LogFormat("[The Grand Grimoire #{0}] The number of ports is {1}, so you must start with the letter {2}.", _moduleID, Bomb.GetPorts().Count(), descriptionDespaced[ix].ToString().ToUpperInvariant());
            for (int i = 0; i < 7; i++)
            {
                var letter = descriptionDespaced[ix];
                var convertedNum = "abcdefghijklmnopqrstuvwxyz".IndexOf(letter) + 1;
                while (convertedNum > 7)
                    convertedNum -= 7;
                convertedNum--;
                while (Actions.Contains(convertedNum))
                {
                    convertedNum++;
                    convertedNum %= 7;
                }
                Actions.Add(convertedNum);
                ix++;
                ix %= descriptionDespaced.Length;
            }
            for (int i = 0; i < 7; i++)
                Actions[i] = (Actions[i] + 7) % 8;
            Actions.Add(6);
            Debug.LogFormat("[The Grand Grimoire #{0}] Therefore, the actions to be performed are {1}.", _moduleID, Actions.Select(x => x == 7 ? "Dagger" : SpellInfo[x + Addend][0]).Join(", "));
        }
    }

    void ButtonPress(int pos)
    {
        if ((pos == 0 && Page > 0) || (pos == 1 && Page < 6))
        {
            Audio.PlaySoundAtTransform("page", Buttons[pos].transform);
            Buttons[pos].AddInteractionPunch(0.5f);
            Page += (pos * 2) - 1;
            if ((pos == 0 && Page == 0) || (pos == 1 && Page == 6))
                ButtonSprites[pos].color = new Color();
            else
                ButtonSprites[pos].color = new Color32(255, 69, 0, 255);
            ButtonSprites[1 - pos].color = new Color(0, 0, 0, 1);
            UpdateContents();
        }
        else if (pos == 2)
        {
            Buttons[pos].AddInteractionPunch();
            if (Actions[Completed] == BookContents[Page])
            {
                Debug.LogFormat("[The Grand Grimoire #{0}] You used {1}, which was correct.", _moduleID, SpellInfo[BookContents[Page] + Addend][0]);
                Completed++;
                Audio.PlaySoundAtTransform(GemSprites[BookContents[Page]].name, Buttons[pos].transform);
                Cast[Page] = true;
                if (Completed >= Actions.Count())
                    StartCoroutine(Solve());
                else
                    Gem.color = new Color(1, 1, 1, 0.25f);
            }
            else
            {
                try
                {
                    StopCoroutine(StrikeAnim);
                }
                catch { }
                StrikeAnim = StartCoroutine(Strike("strike spell", Buttons[2].transform));
                Debug.LogFormat("[The Grand Grimoire #{0}] You attempted to use {1}, but I expected you to use {2}. Strike!", _moduleID,
                    SpellInfo[BookContents[Page] + Addend][0], Actions[Completed] == 7 ? "the dagger" : SpellInfo[Actions[Completed] + Addend][0]);
            }
        }
        else if (pos == 3)
        {
            Buttons[pos].AddInteractionPunch();
            if (Actions[Completed] == 7)
            {
                Debug.LogFormat("[The Grand Grimoire #{0}] You used the dagger, which was correct.", _moduleID);
                Completed++;
                Audio.PlaySoundAtTransform("stab", Buttons[pos].transform);
                Bloodied[Page] = true;
                if (Completed >= Actions.Count())
                    StartCoroutine(Solve());
                else if (!_Settings.RemoveBlood)
                    Blood.color = new Color(1, 1, 1, 1);
            }
            else
            {
                try
                {
                    StopCoroutine(StrikeAnim);
                }
                catch { }
                StrikeAnim = StartCoroutine(Strike("strike stab", Buttons[3].transform));
                Debug.LogFormat("[The Grand Grimoire #{0}] You attempted to use the dagger, but I expected you to use {1}. Strike!", _moduleID, SpellInfo[Actions[Completed] + Addend][0]);
            }
        }
    }

    void UpdateContents()
    {
        for (int i = 0; i < 3; i++)
            Texts[i].text = SpellInfo[BookContents[Page] + Addend][i];
        Gem.sprite = GemSprites[BookContents[Page] + Addend];
        for (int i = 0; i < 7; i++)
            Dots[i].sprite = Symbols[0];
        Dots[Page].sprite = Symbols[1];
        if (!_Settings.RemoveBlood)
        {
            if (Bloodied[Page])
                Blood.color = new Color(1, 1, 1, 1);
            else if (Bloodied[Page - 1 < 0 ? Page : Page - 1] || Bloodied[Page + 1 > 6 ? Page : Page + 1])
                Blood.color = new Color(1, 1, 1, 0.5f);
            else
                Blood.color = new Color();
        }
        else
        {
            if (Bloodied[Page])
                Buttons[3].GetComponentsInChildren<SpriteRenderer>()[1].color = new Color(0, 0, 0, 0.25f);
            else
                Buttons[3].GetComponentsInChildren<SpriteRenderer>()[1].color = new Color(0, 0, 0, 1);
        }
        if (Cast[Page])
            Gem.color = new Color(1, 1, 1, 0.25f);
        else
            Gem.color = new Color(1, 1, 1, 1);
    }

    private IEnumerator Solve(float duration = 1f)
    {
        Debug.LogFormat("[The Grand Grimoire #{0}] Module solved!", _moduleID);
        Solved = true;
        Module.HandlePass();
        Blood.color = new Color();
        Audio.PlaySoundAtTransform("solve", Gem.transform);
        StatusLight.material.color = new Color(0, 1, 0);
        Glow.color = new Color(0, 1, 0, 0.5f);
        for (int i = 0; i < 2; i++)
            Buttons[i].GetComponent<SpriteRenderer>().color = new Color();
        float timer = 0;
        while (timer < duration)
        {
            yield return null;
            timer += Time.deltaTime;
            for (int i = 0; i < 3; i++)
                Texts[i].color = Color.Lerp(new Color(0, 0, 0, 1), new Color(), timer / duration);
            for (int i = 0; i < 7; i++)
                Dots[i].color = Color.Lerp(new Color(0, 0, 0, 1), new Color(), timer / duration);
            Buttons[2].GetComponentInChildren<TextMesh>().color = Color.Lerp(new Color(0, 0, 0, 1), new Color(), timer / duration);
            Buttons[3].GetComponentsInChildren<SpriteRenderer>()[1].color = Color.Lerp(new Color(0, 0, 0, 1), new Color(), timer / duration);
            Gem.color = Color.Lerp(new Color(1, 1, 1, 1), new Color(1, 1, 1, 0), timer / duration);
        }
        for (int i = 0; i < 3; i++)
            Texts[i].color = new Color();
        for (int i = 0; i < 7; i++)
            Dots[i].color = new Color();
        Buttons[2].GetComponentInChildren<TextMesh>().color = new Color();
        Buttons[3].GetComponentsInChildren<SpriteRenderer>()[1].color = new Color();
        Gem.color = new Color();
    }

    private IEnumerator Strike(string sound, Transform transform)
    {
        Module.HandleStrike();
        Audio.PlaySoundAtTransform(sound, transform);
        StatusLight.material.color = new Color(1, 0, 0);
        Glow.color = new Color(1, 0, 0, 0.5f);
        float timer = 0;
        while (timer < 1)
        {
            yield return null;
            timer += Time.deltaTime;
        }
        StatusLight.material.color = new Color();
        Glow.color = new Color();
    }

    private IEnumerator AnimStatus(float speed = 5)
    {
        while (true)
        {
            float timer = 0;
            while (timer < speed)
            {
                yield return null;
                timer += Time.deltaTime;
                StatusLight.transform.localEulerAngles = Vector3.Lerp(new Vector3(-90, 0, 0), new Vector3(-90, 360, 0), timer / speed);
            }
        }
    }

#pragma warning disable 414
    private string TwitchHelpMessage = "Use '!{0} godoor famalia dagger' to perform the specified actions.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToLowerInvariant();
        var commandArray = command.Split(' ');
        var validcmds = new[] { "ignaize", "dimere", "goldor", "famalia", "godoor", "fainfol", "granwyrm", "dagger" };

        for (int i = 0; i < commandArray.Length; i++)
        {
            if (!validcmds.Contains(commandArray[i]))
            {
                yield return "sendtochaterror Invalid command.";
                yield break;
            }
        }
        yield return null;
        for (int i = 0; i < commandArray.Length; i++)
        {
            if (commandArray[i] == "dagger")
                Buttons[3].OnInteract();
            else
            {
                var targetPage = BookContents.IndexOf(Array.IndexOf(validcmds, commandArray[i]));
                if (targetPage != Page)
                {
                    var buttonToPress = targetPage < Page ? Buttons[0] : Buttons[1];
                    while (targetPage != Page)
                    {
                        buttonToPress.OnInteract();
                        yield return new WaitForSeconds(0.1f);
                    }
                }
                Buttons[2].OnInteract();
                yield return new WaitForSeconds(0.2f);
            }
        }
    }
    IEnumerator TwitchHandleForcedSolve()
    {
        for (int i = 0; i < Actions.Count(); i++)
        {
            if (Actions[i] == 7)
                Buttons[3].OnInteract();
            else
            {
                var targetPage = BookContents.IndexOf(Actions[i]);
                if (targetPage != Page)
                {
                    var buttonToPress = targetPage < Page ? Buttons[0] : Buttons[1];
                    while (targetPage != Page)
                    {
                        buttonToPress.OnInteract();
                        yield return null;
                    }
                }
                Buttons[2].OnInteract();
                yield return null;
            }
        }
    }
}