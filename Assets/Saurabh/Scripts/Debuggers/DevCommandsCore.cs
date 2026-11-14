using System;
using UnityEngine;

/// <summary>
/// The first set of developer console commands: money, reputation, day, cats and customers.
/// Kept in one file, away from the game scripts, so the game code stays clean and every
/// cheat is easy to find. New systems (cages, drain...) can add theirs here or in their own script.
/// Put this next to DevConsole (for example on Managers).
/// </summary>
public class DevCommandsCore : MonoBehaviour
{
    private const string Source = "DevConsole";   // shows up in the economy logs, so you know a cheat did it

    private void Start()
    {
        if (!Debug.isDebugBuild) return;   // no cheats in tester builds

        AddEconomyCommands();
        AddDayCommands();
        AddCatCommands();
        AddCustomerCommands();
    }

    // ==========================================
    // MONEY AND REPUTATION
    // ==========================================

    private void AddEconomyCommands()
    {
        DevConsole.Add("money", "money <amount> · add GP (negative removes)", args =>
        {
            if (!DevConsole.TryGetInt(args, 0, out int amount))
                return DevConsole.Error("money needs a number, e.g. money 500");

            CoreEconomySystem.Instance.ModifyGP(amount, Source);
            return $"{amount:+#;-#;0} GP (now {CoreEconomySystem.Instance.GetGP(Source)} GP)";
        });

        DevConsole.Add("setmoney", "setmoney <amount> · set GP to an exact value", args =>
        {
            if (!DevConsole.TryGetInt(args, 0, out int amount) || amount < 0)
                return DevConsole.Error("setmoney needs a number of 0 or more");

            CoreEconomySystem.Instance.OverwriteGP(amount, Source);
            return $"GP set to {amount}";
        });

        DevConsole.Add("ledger", "ledger · where today's GP came from and went, and who arrived", args =>
            CoreEconomySystem.Instance.LedgerText());

        DevConsole.Add("rep", "rep <amount> · add reputation (negative removes, stops at 0)", args =>
        {
            if (!DevConsole.TryGetInt(args, 0, out int amount))
                return DevConsole.Error("rep needs a number, e.g. rep 150");

            CoreEconomySystem.Instance.ModifyReputation(amount, Source);
            return $"{amount:+#;-#;0} reputation (now {CoreEconomySystem.Instance.GetReputation(Source)})";
        });
    }

    // ==========================================
    // DAY AND TIME
    // ==========================================

    private void AddDayCommands()
    {
        DevConsole.Add("status", "status · day, phase, GP, reputation and cats", args =>
        {
            DayManager day = DayManager.Instance;
            string dayText = day != null ? $"Day {day.Day} · {day.Phase}" : "No DayManager";
            return $"{dayText} · {CoreEconomySystem.Instance.GetGP(Source)} GP · " +
                   $"rep {CoreEconomySystem.Instance.GetReputation(Source)} · " +
                   $"{CageManager.Instance.CatCount}/{CageManager.Instance.allCages.Count} cats · speed {Time.timeScale}x";
        });

        DevConsole.Add("lastcall", "lastcall · skip to closing time", args =>
            DayManager.Instance.DevSkipToLastCall()
                ? "Skipped to closing time"
                : DevConsole.Error("Only works while the shop is open"));

        DevConsole.Add("endday", "endday · end the day now and show the summary", args =>
            DayManager.Instance.DevEndDay()
                ? "Day ended"
                : DevConsole.Error("Only works while the shop is open or at closing time"));
    }

    // ==========================================
    // CATS
    // ==========================================

    private void AddCatCommands()
    {
        DevConsole.Add("spawncat", "spawncat [feed] [clean] [traits] · e.g. spawncat 4 4 skittish chonky calico", args =>
        {
            // Numbers are care levels (first = feed, second = clean). Words are traits, in any order.
            // Anything you leave out is random.
            int feed = 0, clean = 0, numbersSeen = 0;
            CatBehavior? behavior = null;
            CatBody? body = null;
            CatPattern? pattern = null;

            foreach (string word in args)
            {
                if (int.TryParse(word, out int number))
                {
                    if (numbersSeen == 0) feed = number;
                    else if (numbersSeen == 1) clean = number;
                    else return DevConsole.Error("Only two numbers: feed and clean");
                    numbersSeen++;
                }
                else if (TryTrait(word, out CatBehavior b)) behavior = b;
                else if (TryTrait(word, out CatBody bo)) body = bo;
                else if (TryTrait(word, out CatPattern p)) pattern = p;
                else return DevConsole.Error($"Unknown trait '{word}'. Try: {TraitList<CatBehavior>()} · " +
                                             $"{TraitList<CatBody>()} · {TraitList<CatPattern>()}");
            }

            Transform cage = CageManager.Instance.GetAvailableCage();
            if (cage == null) return DevConsole.Error("Every cage is full");

            // Same steps as StarterCats: make the cat, set care AFTER it's made, then put it in the cage
            BaseCat cat = CatFactory.Instance.GenerateRandomCat(cage.position, behavior, body, pattern);
            cat.feedingLevel = Mathf.Clamp(feed, 0, BaseCat.MaxLevel);
            cat.cleanlinessLevel = Mathf.Clamp(clean, 0, BaseCat.MaxLevel);
            CageManager.Instance.OccupyCage(cage, cat);

            string tier = CareRules.Name(CareRules.TierOf(cat.TotalCare));
            return $"Spawned a {tier} {cat.Pattern} {cat.Body} {cat.Behavior} cat ({cat.feedingLevel}/{cat.cleanlinessLevel}) in {cage.name}";
        });

        DevConsole.Add("maxcare", "maxcare · every cat to 6/6 (Purrfect)", args => SetAllCare(BaseCat.MaxLevel));
        DevConsole.Add("clearcare", "clearcare · every cat to 0/0", args => SetAllCare(0));
    }

    // Turns a word like "skittish" into the matching trait (any upper/lower case).
    // "where T : struct, Enum" means this works for any enum: CatBehavior, CatBody or CatPattern.
    private static bool TryTrait<T>(string word, out T trait) where T : struct, Enum
    {
        // Enum.TryParse also accepts numbers ("2"), so we also check the word is a real name
        return Enum.TryParse(word, true, out trait) && Enum.IsDefined(typeof(T), trait)
               && !int.TryParse(word, out _);
    }

    // "playful/lazy/angry/skittish" for the error message
    private static string TraitList<T>() where T : struct, Enum
    {
        return string.Join("/", Enum.GetNames(typeof(T))).ToLowerInvariant();
    }

    private string SetAllCare(int level)
    {
        BaseCat[] cats = FindObjectsByType<BaseCat>(FindObjectsSortMode.None);
        foreach (BaseCat cat in cats)
        {
            cat.feedingLevel = level;
            cat.cleanlinessLevel = level;
        }
        return $"Set {cats.Length} cats to {level}/{level}";
    }

    // ==========================================
    // CUSTOMERS
    // ==========================================

    private void AddCustomerCommands()
    {
        DevConsole.Add("customer", "customer <taker|giver|stray> · send one now", args =>
        {
            // Enum.TryParse turns the text "giver" into CustomerKind.Giver (true = ignore upper/lower case)
            if (args.Length == 0 || !Enum.TryParse(args[0], true, out CustomerSpawner.CustomerKind kind))
                return DevConsole.Error("Usage: customer taker | giver | stray");

            CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
            if (spawner == null) return DevConsole.Error("No CustomerSpawner in the scene");

            // A Taker with nothing to adopt has nothing to do, so the normal game never sends one
            if (kind == CustomerSpawner.CustomerKind.Taker && !CageManager.Instance.HasCatForAdoption())
                return DevConsole.Error("No cat is waiting for adoption. Try spawncat first.");

            return spawner.SpawnCustomer(kind) ? $"Sent a {kind}" : DevConsole.Error("The queue is full");
        });

        DevConsole.Add("spawning", "spawning <on|off> · start or stop customers arriving", args =>
        {
            CustomerSpawner spawner = FindFirstObjectByType<CustomerSpawner>();
            if (spawner == null) return DevConsole.Error("No CustomerSpawner in the scene");

            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            if (mode == "on")  { spawner.StartSpawning(); return "Customers will arrive"; }
            if (mode == "off") { spawner.StopSpawning();  return "Customers stopped"; }
            return DevConsole.Error("Usage: spawning on | off");
        });
    }
}