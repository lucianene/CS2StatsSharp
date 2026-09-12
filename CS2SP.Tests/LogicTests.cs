using System.Text.Json.Nodes;
using CS2SP.Logic;
using Xunit;

namespace CS2SP.Tests;

public class ModProfilesTests
{
    [Fact]
    public void Unknown_or_empty_falls_back_to_matchmaking()
    {
        Assert.Equal(ModProfiles.Matchmaking, ModProfiles.Resolve(null));
        Assert.Equal(ModProfiles.Matchmaking, ModProfiles.Resolve(""));
        Assert.Equal(ModProfiles.Matchmaking, ModProfiles.Resolve("retake"));
        Assert.Equal(ModProfiles.Matchmaking, ModProfiles.Resolve("zombie"));
    }

    [Fact]
    public void Quoted_and_padded_cfg_values_still_resolve()
    {
        Assert.Equal(ModProfiles.Aim, ModProfiles.Resolve("\"aim\"                           "));
        Assert.Equal(ModProfiles.Deathmatch, ModProfiles.Resolve("\"deathmatch\"                           // comment"));
    }

    [Theory]
    [InlineData("matchmaking")]
    [InlineData("MATCHMAKING")]
    [InlineData("Deathmatch")]
    [InlineData("deathmatch")]
    [InlineData("aim")]
    [InlineData("AIM")]
    public void Resolve_is_case_insensitive(string name)
    {
        var p = ModProfiles.Resolve(name);
        Assert.Equal(name, p.Name, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deathmatch_is_ffa_and_resets_on_spawn()
    {
        var dm = ModProfiles.Resolve("deathmatch");
        Assert.True(dm.FreeForAll);
        Assert.True(dm.ResetStateOnSpawn);
    }

    [Fact]
    public void Matchmaking_is_not_ffa()
    {
        var mm = ModProfiles.Resolve("matchmaking");
        Assert.False(mm.FreeForAll);
        Assert.False(mm.ResetStateOnSpawn);
    }

    [Fact]
    public void Aim_resets_on_spawn_and_is_not_ffa()
    {
        var aim = ModProfiles.Resolve("aim");
        Assert.False(aim.FreeForAll);
        Assert.True(aim.ResetStateOnSpawn);
    }
}

public class WeaponKindsTests
{
    [Theory]
    [InlineData("glock", true)]
    [InlineData("deagle", true)]
    [InlineData("ak47", false)]
    [InlineData(null, false)]
    public void Pistols(string? weapon, bool expected) =>
        Assert.Equal(expected, WeaponKinds.IsPistol(weapon));

    [Theory]
    [InlineData("awp", true)]
    [InlineData("ssg08", true)]
    [InlineData("ak47", false)]
    public void Snipers(string weapon, bool expected) =>
        Assert.Equal(expected, WeaponKinds.IsSniper(weapon));

    [Fact]
    public void Delayed_utility_and_fire()
    {
        Assert.True(WeaponKinds.IsDelayedUtility("hegrenade"));
        Assert.True(WeaponKinds.IsUtility("inferno"));
        Assert.True(WeaponKinds.IsFire("molotov"));
        Assert.False(WeaponKinds.IsFire("hegrenade"));
        Assert.True(WeaponKinds.IsBombKill("planted_c4"));
        Assert.False(WeaponKinds.IsDelayedUtility("ak47"));
        Assert.True(WeaponKinds.IsPistol("weapon_deagle"));
        Assert.True(WeaponKinds.IsPistol("hkp2000"));
        Assert.True(WeaponKinds.IsSniper("weapon_awp"));
        Assert.True(WeaponKinds.IsDelayedUtility("incgrenade"));
        Assert.True(WeaponKinds.IsFire("incgrenade"));
        Assert.True(WeaponKinds.IsBombKill("weapon_planted_c4"));
        Assert.Equal("p2000", WeaponKinds.Normalize("weapon_hkp2000"));
    }
}

public class DamageClampTests
{
    [Fact]
    public void Caps_overkill_to_remaining_health()
    {
        Assert.Equal(40, DamageClamp.ToCredit(140, 40, 0));
        Assert.Equal(0, DamageClamp.ToCredit(50, 0, 0));
        Assert.Equal(0, DamageClamp.ToCredit(-3, 80, 80));
        Assert.Equal(25, DamageClamp.ToCredit(25, 80, 55));
    }

    [Fact]
    public void Prefers_observed_hp_when_it_disagrees_by_one() =>
        Assert.Equal(26, DamageClamp.ToCredit(dmgHealth: 25, healthBefore: 100, healthAfter: 74));

    [Fact]
    public void Caps_event_damage_at_remaining_hp() =>
        Assert.Equal(10, DamageClamp.ToCredit(dmgHealth: 80, healthBefore: 10, healthAfter: 0));

    [Fact]
    public void Ignores_large_untracked_world_delta() =>
        Assert.Equal(15, DamageClamp.ToCredit(dmgHealth: 15, healthBefore: 80, healthAfter: 20));

    [Fact]
    public void Unseen_victim_defaults_to_100()
    {
        var map = new Dictionary<int, int>();
        Assert.Equal(DamageClamp.DefaultHealth, DamageClamp.HealthBefore(map, 3));
        map[3] = 0;
        Assert.Equal(0, DamageClamp.HealthBefore(map, 3));
    }
}

public class KillCreditTests
{
    [Fact]
    public void Dead_killer_is_skipped_unless_ffa_teamkill_path()
    {
        Assert.Equal(KillOutcome.Skip, KillCredit.ClassifyDeath(killerDeadThisRound: true, sameTeam: false, freeForAll: false));
        Assert.Equal(KillOutcome.TeamKill, KillCredit.ClassifyDeath(false, sameTeam: true, freeForAll: false));
        Assert.Equal(KillOutcome.Kill, KillCredit.ClassifyDeath(false, sameTeam: true, freeForAll: true));
        Assert.Equal(KillOutcome.Kill, KillCredit.ClassifyDeath(false, sameTeam: false, freeForAll: false));
        Assert.Equal(KillOutcome.Suicide, KillCredit.ClassifyDeath(false, sameTeam: true, freeForAll: false, suicide: true));
        Assert.Equal(KillOutcome.Suicide, KillCredit.ClassifyDeath(killerDeadThisRound: true, sameTeam: true, freeForAll: false, suicide: true));
    }

    [Fact]
    public void Multi_kills_fire_at_3_4_5()
    {
        var s = new PlayerStats();
        KillCredit.ApplyMultiKills(s, 2);
        Assert.Equal(0, s.TripleKills);
        KillCredit.ApplyMultiKills(s, 3);
        KillCredit.ApplyMultiKills(s, 4);
        KillCredit.ApplyMultiKills(s, 5);
        Assert.Equal(1, s.TripleKills);
        Assert.Equal(1, s.QuadroKills);
        Assert.Equal(1, s.PentaKills);
    }

    [Fact]
    public void Damage_and_flash_gates()
    {
        Assert.False(KillCredit.ShouldCreditDamage(attackerDeadThisRound: true, sameTeam: false, freeForAll: false, delayedUtility: false));
        Assert.True(KillCredit.ShouldCreditDamage(true, sameTeam: false, freeForAll: false, delayedUtility: true));
        Assert.False(KillCredit.ShouldCreditDamage(false, sameTeam: true, freeForAll: false, delayedUtility: false));
        Assert.True(KillCredit.ShouldCreditDamage(false, sameTeam: true, freeForAll: true, delayedUtility: false));

        Assert.False(KillCredit.ShouldCreditFlash(attackerIsVictim: true, sameTeam: false, freeForAll: true, 1.5f));
        Assert.False(KillCredit.ShouldCreditFlash(false, sameTeam: true, freeForAll: false, 1.5f));
        Assert.False(KillCredit.ShouldCreditFlash(false, sameTeam: false, freeForAll: true, 0f));
        Assert.True(KillCredit.ShouldCreditFlash(false, sameTeam: true, freeForAll: true, 0.4f));
    }

    [Fact]
    public void Dink_is_head_hit_that_did_not_kill()
    {
        Assert.True(KillCredit.IsDink(1, 40));
        Assert.False(KillCredit.IsDink(1, 0));
        Assert.False(KillCredit.IsDink(2, 80));
        Assert.True(KillCredit.IsClutch(0));
        Assert.False(KillCredit.IsClutch(1));
    }
}

public class PlayerStatsStoreTests
{
    [Fact]
    public void Slot_replace_and_round_reset()
    {
        var store = new PlayerStatsStore();
        var a = store.Replace(1, isBot: false);
        a.Kills = 4;
        a.RoundKills = 2;
        a.DeadThisRound = true;
        a.AddGiven(3, 40);
        store.ResetAllRounds();
        Assert.Equal(4, a.Kills);
        Assert.Equal(0, a.RoundKills);
        Assert.False(a.DeadThisRound);
        Assert.Empty(a.RoundGivenDamage);
        store.Remove(1);
        Assert.Null(store.Get(1));
        var stray = store.GetOrCreate(99);
        Assert.Null(store.Get(99));
        stray.Kills = 1;
        Assert.Null(store.Get(99));
    }

    [Fact]
    public void Kdr_and_hs_percent()
    {
        var s = new PlayerStats { Kills = 10, Deaths = 4, Headshots = 3 };
        Assert.Equal(2.5f, s.Kdr);
        Assert.Equal(30f, s.HeadshotPercentage, 3);
        var undefeated = new PlayerStats { Kills = 7 };
        Assert.Equal(7f, undefeated.Kdr);
        Assert.Equal(0f, undefeated.HeadshotPercentage);
    }

    [Fact]
    public void Unique_kills_are_a_set()
    {
        var s = new PlayerStats();
        s.UniqueKills.Add(1);
        s.UniqueKills.Add(1);
        s.UniqueKills.Add(2);
        Assert.Equal(2, s.UniqueKills.Count);
    }
}

public class StatsPayloadTests
{
    [Fact]
    public void Empty_player_list_is_skipped() =>
        Assert.Null(StatsPayload.Build([], "de_dust2", 24));

    [Fact]
    public void Shape_matches_mod_stats_contract()
    {
        var stats = new PlayerStats
        {
            Kills = 5,
            Deaths = 2,
            Headshots = 2,
            TeamKills = 0,
            Hits = 12,
            Mvps = 1,
            TripleKills = 1,
            UtilityDamage = 30,
            FireDamage = 10,
            Dinks = 1,
            FirstKills = 1,
            ClutchKills = 1,
            PistolKills = 1,
            SniperKills = 2,
            BlindKills = 0,
            BombKills = 0,
            ChickenKills = 0,
            BombPlants = 1,
            BombDefuses = 0,
            BombExplodes = 0,
            EnemiesFlashed = 3
        };
        stats.UniqueKills.Add(76561198000000000UL);

        var body = StatsPayload.Build(
        [
            new PlayerUpload
            {
                SteamId = "76561198000000001",
                Name = "tester",
                Team = 3,
                Round = 4,
                RoundWin = true,
                Stats = stats,
                Assists = 3,
                TotalDamage = 400,
                Money = 16000
            }
        ], "de_mirage", 24);

        Assert.NotNull(body);
        Assert.Equal("cs2", body!["game"]!.GetValue<string>());
        Assert.Equal("de_mirage", body["match"]!["map"]!.GetValue<string>());
        Assert.Equal(24, body["match"]!["max_rounds"]!.GetValue<int>());

        var row = body["stats"]!["76561198000000001"]!.AsObject();
        Assert.Equal("76561198000000001", row["steam_id"]!.GetValue<string>());
        Assert.Equal("tester", row["name"]!.GetValue<string>());
        Assert.Equal(4, row["round"]!.GetValue<int>());
        Assert.True(row["round_win"]!.GetValue<bool>());
        Assert.Equal(5, row["kills"]!.GetValue<int>());
        Assert.Equal(3, row["assists"]!.GetValue<int>());
        Assert.Equal(400, row["total_damage"]!.GetValue<int>());
        Assert.Equal(100f, row["adr"]!.GetValue<float>(), 3);
        Assert.Equal(16000, row["money"]!.GetValue<int>());
        Assert.Equal(1, row["unique_kills"]!.GetValue<int>());
        Assert.Equal(2.5f, row["kdr"]!.GetValue<float>(), 3);
    }

    [Fact]
    public void Periodic_upload_has_no_round_winner()
    {
        var row = StatsPayload.ToPlayerObject(new PlayerUpload
        {
            SteamId = "1",
            Team = 2,
            Round = 0,
            RoundWin = false,
            Stats = new PlayerStats(),
            Assists = 0,
            TotalDamage = 50,
            Money = 0
        });
        Assert.False(row["round_win"]!.GetValue<bool>());
        Assert.Equal(0f, row["adr"]!.GetValue<float>());
    }
}

public class CvarTextTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("aim", "aim")]
    [InlineData("  aim  ", "aim")]
    [InlineData("\"aim\"", "aim")]
    [InlineData("\"aim\"                           ", "aim")]
    [InlineData("\"aim\"                           // REQUIRED: comment", "aim")]
    [InlineData("\"https://api.playcup.ro/v1/mod-stats/\"   ", "https://api.playcup.ro/v1/mod-stats/")]
    public void Clean_strips_quotes_padding_and_trailing_cfg_comments(string? raw, string expected) =>
        Assert.Equal(expected, CvarText.Clean(raw));

    [Fact]
    public void CleanHeader_drops_control_characters()
    {
        Assert.Equal("aim", CvarText.CleanHeader("\"aim\"\r\nX-Injected: 1"));
        Assert.Equal("aim", CvarText.CleanHeader("aim\t"));
    }
}

public class UploadUrlTests
{
    [Fact]
    public void Empty_address_is_not_configured()
    {
        Assert.False(UploadUrl.IsConfigured(null));
        Assert.False(UploadUrl.IsConfigured(""));
        Assert.False(UploadUrl.IsConfigured("   "));
        Assert.True(UploadUrl.IsConfigured("https://api-test.playcup.ro/v1/mod-stats/"));
    }

    [Fact]
    public void Query_string_matches_cpp_param_names()
    {
        var url = UploadUrl.Build("https://api-test.playcup.ro/v1/mod-stats/", "abc", "deathmatch", "deathmatch");
        Assert.Equal(
            "https://api-test.playcup.ro/v1/mod-stats/?match_id=abc&mod=deathmatch&game_mode=deathmatch",
            url);
    }

    [Fact]
    public void Special_characters_are_escaped()
    {
        var url = UploadUrl.Build("https://x/", "a b", "death match", "a&b");
        Assert.Contains("match_id=a%20b", url);
        Assert.Contains("mod=death%20match", url);
        Assert.Contains("game_mode=a%26b", url);
    }

    [Fact]
    public void Quoted_padded_cvars_build_a_valid_absolute_uri()
    {
        var url = UploadUrl.Build(
            "\"https://api.playcup.ro/v1/mod-stats/\"   ",
            "\"\"",
            "\"aim\"                           ",
            "\"aim\"               ");
        Assert.Equal(
            "https://api.playcup.ro/v1/mod-stats/?match_id=&mod=aim&game_mode=aim",
            url);
        Assert.True(Uri.TryCreate(url, UriKind.Absolute, out var uri));
        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
    }

    [Fact]
    public void Existing_query_string_is_appended_with_ampersand()
    {
        var url = UploadUrl.Build("https://x/v1/mod-stats/?src=cfg", "", "aim", "aim");
        Assert.Equal("https://x/v1/mod-stats/?src=cfg&match_id=&mod=aim&game_mode=aim", url);
    }
}

public class PeriodicClockTests
{
    [Fact]
    public void Arms_on_first_tick_and_after_curtime_reset()
    {
        var clock = new PeriodicClock();
        Assert.False(clock.TryFire(10f, 30f));
        Assert.False(clock.TryFire(20f, 30f));
        Assert.True(clock.TryFire(40.5f, 30f));
        Assert.False(clock.TryFire(50f, 30f));
        Assert.True(clock.TryFire(70.5f, 30f));

        Assert.False(clock.TryFire(1f, 30f));
        Assert.False(clock.TryFire(20f, 30f));
        Assert.True(clock.TryFire(31.1f, 30f));
    }

    [Fact]
    public void Interval_floor_is_one_second()
    {
        var clock = new PeriodicClock();
        Assert.False(clock.TryFire(0f, 0.1f));
        Assert.False(clock.TryFire(0.5f, 0.1f));
        Assert.True(clock.TryFire(1.0f, 0.1f));
    }
}

public class RoundEndReasonsTests
{
    [Fact]
    public void Game_start_matches_cpp() => Assert.Equal(16, RoundEndReasons.GameStart);
}
