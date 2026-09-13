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
        Assert.Equal(ModProfiles.Matchmaking, ModProfiles.Resolve("foobar"));
        Assert.Equal(ModProfiles.Matchmaking, ModProfiles.Resolve("retake-plus"));
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
    [InlineData("casual")]
    [InlineData("zombie")]
    [InlineData("retake")]
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

    [Fact]
    public void Casual_is_round_based_like_matchmaking()
    {
        var casual = ModProfiles.Resolve("casual");
        Assert.Equal("casual", casual.Name);
        Assert.False(casual.FreeForAll);
        Assert.False(casual.ResetStateOnSpawn);
    }

    [Fact]
    public void Zombie_is_team_scoring_with_respawn()
    {
        var zm = ModProfiles.Resolve("zombie");
        Assert.False(zm.FreeForAll);
        Assert.True(zm.ResetStateOnSpawn);
    }

    [Fact]
    public void Quoted_casual_and_zombie_cfg_values_still_resolve()
    {
        Assert.Equal(ModProfiles.Casual, ModProfiles.Resolve("\"casual\"                           "));
        Assert.Equal(ModProfiles.Zombie, ModProfiles.Resolve("\"zombie\"                           // comment"));
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
        Assert.True(WeaponKinds.IsRifle("ak47"));
        Assert.True(WeaponKinds.IsRifle("weapon_m4a1_silencer"));
        Assert.True(WeaponKinds.IsSmg("mp9"));
        Assert.True(WeaponKinds.IsShotgun("xm1014"));
        Assert.True(WeaponKinds.IsKnife("knife"));
        Assert.True(WeaponKinds.IsKnife("weapon_knife_t"));
        Assert.True(WeaponKinds.IsZeus("taser"));
        Assert.True(WeaponKinds.IsHe("hegrenade"));
        var thrown = new PlayerStats();
        WeaponKinds.CountThrow(thrown, "weapon_flashbang");
        WeaponKinds.CountThrow(thrown, "hegrenade");
        WeaponKinds.CountThrow(thrown, "smokegrenade");
        WeaponKinds.CountThrow(thrown, "molotov");
        WeaponKinds.CountThrow(thrown, "decoy");
        Assert.Equal(1, thrown.FlashThrown);
        Assert.Equal(1, thrown.HeThrown);
        Assert.Equal(1, thrown.SmokeThrown);
        Assert.Equal(1, thrown.MolotovThrown);
        Assert.Equal(1, thrown.DecoyThrown);
        Assert.Equal("p2000", WeaponKinds.Normalize("weapon_hkp2000"));
    }
}

public class KillFlavorTests
{
    [Fact]
    public void Death_flags_and_weapon_classes()
    {
        var s = new PlayerStats();
        KillFlavor.Apply(s, "awp", noscope: true, thruSmoke: true, inAir: true, attackerBlind: true, penetrated: 2, distance: 41.5f);
        Assert.Equal(1, s.SniperKills);
        Assert.Equal(1, s.NoScopeKills);
        Assert.Equal(1, s.SmokeKills);
        Assert.Equal(1, s.AirKills);
        Assert.Equal(1, s.WallbangKills);
        Assert.Equal(1, s.BlindKills);
        Assert.Equal(41.5f, s.LongestKillDistance);

        KillFlavor.Apply(s, "ak47", noscope: true, thruSmoke: false, inAir: false, attackerBlind: false, penetrated: 0, distance: 10f);
        Assert.Equal(1, s.RifleKills);
        Assert.Equal(1, s.NoScopeKills);
        Assert.Equal(41.5f, s.LongestKillDistance);

        KillFlavor.Apply(s, "knife", noscope: false, thruSmoke: false, inAir: false, attackerBlind: false, penetrated: 0, distance: 1f);
        Assert.Equal(1, s.KnifeKills);

        KillFlavor.Apply(s, "inferno", noscope: false, thruSmoke: false, inAir: false, attackerBlind: false, penetrated: 0, distance: 4f);
        Assert.Equal(1, s.MolotovKills);
        KillFlavor.ApplyNotices(s, dominated: 1, revenge: 1, wipe: 1);
        Assert.Equal(1, s.Dominations);
        Assert.Equal(1, s.Revenges);
        Assert.Equal(1, s.TeamWipes);
    }
}

public class PlayerStatsAccuracyTests
{
    [Fact]
    public void Accuracy_uses_shots_on_target_when_present()
    {
        var s = new PlayerStats { ShotsFired = 10, ShotsOnTarget = 4, Hits = 12 };
        Assert.Equal(40f, s.Accuracy, 3);
        s.ShotsFired = 0;
        Assert.Equal(0f, s.Accuracy);
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
    public void Multi_kills_fire_at_2_3_4_5()
    {
        var s = new PlayerStats();
        KillCredit.ApplyMultiKills(s, 2);
        Assert.Equal(1, s.DoubleKills);
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

public class SteamIdsTests
{
    [Theory]
    [InlineData(0UL, false)]
    [InlineData(99UL, false)]
    [InlineData(76561197960265727UL, false)]
    [InlineData(76561197960265728UL, true)]
    [InlineData(76561198000000001UL, true)]
    public void Individual_steam64(ulong id, bool expected) =>
        Assert.Equal(expected, SteamIds.IsIndividual(id));
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
    public void Disconnect_keeps_scoreboard_for_upload_and_reconnect_restores_it()
    {
        var store = new PlayerStatsStore();
        var a = store.Bind(2, isBot: false, steam: 76561198000000001UL);
        a.Kills = 8;
        a.Assists = 3;
        a.Damage = 400;
        a.Name = "alice";
        a.Team = 3;
        store.Disconnect(2);
        Assert.Null(store.Get(2));
        Assert.Empty(store.HumansForUpload());
        var parked = Assert.Single(store.HumansForUpload(includeDisconnected: true));
        Assert.Equal(8, parked.Kills);
        Assert.True(parked.Disconnected);

        var bot = store.Bind(2, isBot: true);
        Assert.True(bot.IsBot);
        Assert.Empty(store.HumansForUpload());
        Assert.Equal(8, Assert.Single(store.HumansForUpload(includeDisconnected: true)).Kills);

        var back = store.Bind(5, isBot: false, steam: 76561198000000001UL);
        Assert.Same(parked, back);
        Assert.False(back.Disconnected);
        Assert.Equal(8, back.Kills);
        Assert.Equal(5, back.Slot);
        Assert.Equal(8, Assert.Single(store.HumansForUpload()).Kills);
    }

    [Fact]
    public void Leaver_round_and_round_win_are_frozen()
    {
        var s = new PlayerStats { Team = 2, Disconnected = true, ReportedRound = 3, Damage = 400 };
        Assert.Equal(3, s.RoundForUpload(8));
        Assert.False(s.RoundWinForUpload(2));
        Assert.Equal(3, s.ReportedRound);

        var live = new PlayerStats { Team = 3 };
        Assert.Equal(4, live.RoundForUpload(4));
        Assert.True(live.RoundWinForUpload(3));
        Assert.Equal(4, live.ReportedRound);
    }

    [Fact]
    public void Round_reset_does_not_mutate_a_parked_leaver()
    {
        var store = new PlayerStatsStore();
        var a = store.Bind(1, isBot: false, steam: 9);
        a.RoundKills = 4;
        a.DeadThisRound = true;
        a.Kills = 12;
        store.Disconnect(1);
        store.ResetAllRounds();
        Assert.Equal(4, a.RoundKills);
        Assert.True(a.DeadThisRound);
        Assert.Equal(12, a.Kills);
    }

    [Fact]
    public void Late_steam_attach_absorbs_into_parked_record()
    {
        var store = new PlayerStatsStore();
        var parked = store.Bind(1, isBot: false, steam: 42);
        parked.Kills = 10;
        store.Disconnect(1);

        var stub = store.Bind(4, isBot: false, steam: 0);
        stub.Kills = 1;
        var merged = store.AttachSteam(4, 42);
        Assert.Same(parked, merged);
        Assert.Equal(11, merged.Kills);
        Assert.False(merged.Disconnected);
    }

    [Fact]
    public void NoteSteam_does_not_replace_parked_score_with_zero_stub()
    {
        var store = new PlayerStatsStore();
        var live = store.Bind(2, isBot: false, steam: 76561198000000099UL);
        live.Kills = 7;
        live.Assists = 4;
        live.Damage = 250;
        live.Team = 2;
        live.Name = "bob";
        store.Disconnect(2);

        var ghost = new PlayerStats { Slot = 2, SteamId = 76561198000000099UL };
        var canonical = store.NoteSteam(ghost);
        Assert.Equal(7, canonical.Kills);
        Assert.Equal(4, canonical.Assists);
        Assert.Equal(250, canonical.Damage);
        Assert.Equal("bob", canonical.Name);
        Assert.Empty(store.HumansForUpload());
        Assert.Equal(7, Assert.Single(store.HumansForUpload(includeDisconnected: true)).Kills);
    }

    [Fact]
    public void Resolve_does_not_steal_a_slot_taken_by_another_steam()
    {
        var store = new PlayerStatsStore();
        var alice = store.Bind(3, isBot: false, steam: 1);
        alice.Kills = 5;
        store.Disconnect(3);

        var bob = store.Bind(3, isBot: false, steam: 2);
        bob.Kills = 1;
        var resolved = store.Resolve(3, isBot: false, steam: 1);
        Assert.Same(alice, resolved);
        Assert.Equal(5, alice.Kills);
        Assert.Same(bob, store.Get(3));
        Assert.Equal(1, bob.Kills);
    }

    [Fact]
    public void HumansForUpload_skips_bots_and_non_steam64()
    {
        var store = new PlayerStatsStore();
        store.Bind(1, isBot: true, steam: 76561198000000001UL).Kills = 3;
        store.Bind(2, isBot: false, steam: 99).Kills = 4;
        var human = store.Bind(3, isBot: false, steam: 76561198000000002UL);
        human.Kills = 9;
        Assert.Equal(9, Assert.Single(store.HumansForUpload()).Kills);
    }

    [Fact]
    public void HumansForUpload_omits_disconnected_until_asked()
    {
        var store = new PlayerStatsStore();
        var stay = store.Bind(1, isBot: false, steam: 76561198000000001UL);
        stay.Kills = 2;
        var gone = store.Bind(2, isBot: false, steam: 76561198000000002UL);
        gone.Kills = 9;
        store.Disconnect(2);
        Assert.Equal(2, Assert.Single(store.HumansForUpload()).Kills);
        Assert.Equal(2, store.HumansForUpload(includeDisconnected: true).Count());
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

public class ScoreboardMergeTests
{
    [Fact]
    public void Apply_never_shrinks()
    {
        var stats = new PlayerStats { Kills = 10, Deaths = 3, Assists = 2, Damage = 400 };
        ScoreboardMerge.Apply(stats, kills: 0, deaths: 0, assists: 0, damage: 0, headshots: 0, utilityDamage: 0, enemiesFlashed: 0);
        Assert.Equal(10, stats.Kills);
        Assert.Equal(3, stats.Deaths);
        Assert.Equal(2, stats.Assists);
        Assert.Equal(400, stats.Damage);
        ScoreboardMerge.Apply(stats, kills: 12, deaths: 3, assists: 5, damage: 350, headshots: 4, utilityDamage: 20, enemiesFlashed: 1);
        Assert.Equal(12, stats.Kills);
        Assert.Equal(5, stats.Assists);
        Assert.Equal(400, stats.Damage);
        Assert.Equal(4, stats.Headshots);
    }

    [Fact]
    public void Absorb_keeps_the_higher_kill_flavor_totals()
    {
        var dest = new PlayerStats { NoScopeKills = 2, KnifeKills = 1, LongestKillDistance = 20f };
        var src = new PlayerStats { NoScopeKills = 1, KnifeKills = 4, LongestKillDistance = 41.5f, MolotovKills = 3 };
        ScoreboardMerge.Absorb(dest, src);
        Assert.Equal(2, dest.NoScopeKills);
        Assert.Equal(4, dest.KnifeKills);
        Assert.Equal(3, dest.MolotovKills);
        Assert.Equal(41.5f, dest.LongestKillDistance);
    }

    [Theory]
    [InlineData(8, 0, 8)]
    [InlineData(8, 8, 8)]
    [InlineData(8, 10, 10)]
    [InlineData(8, 2, 10)]
    public void MergeScoreboard_keeps_parked_and_adds_stub_extras(int dest, int src, int expected) =>
        Assert.Equal(expected, ScoreboardMerge.MergeScoreboard(dest, src));
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
            EnemiesFlashed = 3,
            NoScopeKills = 1,
            ShotsFired = 10,
            ShotsOnTarget = 4
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
        Assert.Equal("", body["match"]!["reason"]!.GetValue<string>());

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
        Assert.Equal(1, row["noscope_kills"]!.GetValue<int>());
        Assert.Equal(40f, row["accuracy"]!.GetValue<float>(), 3);
        Assert.False(row.ContainsKey("left"));
    }

    [Fact]
    public void Connect_reason_is_on_the_match_object()
    {
        var body = StatsPayload.Build(
        [
            new PlayerUpload
            {
                SteamId = "76561198000000001",
                Stats = new PlayerStats(),
                Assists = 0,
                TotalDamage = 0,
                Money = 0
            }
        ], "aim_redline", 0, "connect");
        Assert.Equal("connect", body!["match"]!["reason"]!.GetValue<string>());
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

    [Fact]
    public void Leaver_adr_stays_on_the_round_they_left()
    {
        var stats = new PlayerStats { Disconnected = true, ReportedRound = 4, Damage = 400, Team = 2 };
        var round = stats.RoundForUpload(10);
        var row = StatsPayload.ToPlayerObject(new PlayerUpload
        {
            SteamId = "1",
            Team = stats.Team,
            Round = round,
            RoundWin = stats.RoundWinForUpload(2),
            Stats = stats,
            Assists = 1,
            TotalDamage = stats.Damage,
            Money = 800
        });
        Assert.Equal(4, row["round"]!.GetValue<int>());
        Assert.Equal(100f, row["adr"]!.GetValue<float>(), 3);
        Assert.False(row["round_win"]!.GetValue<bool>());
        Assert.Equal(400, row["total_damage"]!.GetValue<int>());
        Assert.False(row.ContainsKey("left"));
    }

    [Fact]
    public void Leaver_payload_sets_left()
    {
        var row = StatsPayload.ToPlayerObject(new PlayerUpload
        {
            SteamId = "76561198000000001",
            Stats = new PlayerStats { Disconnected = true },
            Assists = 0,
            TotalDamage = 0,
            Money = 0,
            Left = true
        });
        Assert.True(row["left"]!.GetValue<bool>());
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

    [Fact]
    public void NoteFire_delays_the_next_periodic_tick()
    {
        var clock = new PeriodicClock();
        Assert.False(clock.TryFire(10f, 30f));
        clock.NoteFire(20f);
        Assert.False(clock.TryFire(40f, 30f));
        Assert.True(clock.TryFire(50.1f, 30f));
    }

    [Fact]
    public void TryFire_zero_after_a_real_last_fire_rearms_like_curtime_reset()
    {
        var clock = new PeriodicClock();
        Assert.False(clock.TryFire(100f, 30f));
        Assert.False(clock.TryFire(0f, 30f));
        Assert.Equal(0f, clock.LastFire);
        Assert.False(clock.TryFire(20f, 30f));
        Assert.True(clock.TryFire(30.1f, 30f));
    }
}

public class PresenceClockTests
{
    [Fact]
    public void Settles_one_second_and_coalesces_join_bursts()
    {
        var clock = new PresenceClock();
        clock.Request(10f);
        Assert.True(clock.Pending);
        Assert.Equal(11f, clock.DueAt);
        Assert.False(clock.TryFire(10.5f));
        Assert.True(clock.TryFire(11f));
        Assert.False(clock.Pending);

        clock.NoteUpload(11f);
        clock.Request(11.2f);
        clock.Request(12f);
        Assert.Equal(16f, clock.DueAt);
        Assert.False(clock.TryFire(15.9f));
        Assert.True(clock.TryFire(16f));
    }

    [Fact]
    public void Reset_clears_pending_and_last_upload()
    {
        var clock = new PresenceClock();
        clock.Request(5f);
        clock.TryFire(6f);
        clock.NoteUpload(6f);
        clock.Reset();
        Assert.False(clock.Pending);
        Assert.Equal(-1f, clock.LastUpload);
        clock.Request(0f);
        Assert.Equal(PresenceClock.Delay, clock.DueAt);
    }
}

public class RoundEndReasonsTests
{
    [Fact]
    public void Game_start_matches_cpp() => Assert.Equal(16, RoundEndReasons.GameStart);
}
