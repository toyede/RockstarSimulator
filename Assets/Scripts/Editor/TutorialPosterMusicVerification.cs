using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ContextStage;
using GameJamKit;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
static class TutorialPosterMusicVerification
{
    const string Root = "E:/UnityProjectArchives/RockstarSimulator/20261101_TutorialPosterMusic/";
    static readonly string[] Keys = { TutorialFlow.TutorialDoneKey, TutorialFlow.SpecialTutorialDoneKey };
    static readonly List<string> Rows = new List<string>();
    static TutorialPosterMusicVerification() { EditorApplication.playModeStateChanged += State; }
    [MenuItem("Tools/Verification/Tutorial Poster Music (Temporary)")]
    static void Start()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode only");
        foreach (var key in Keys) { SessionState.SetBool("TpmHad" + key, Save.Has(key)); SessionState.SetInt("TpmVal" + key, Save.GetInt(key)); }
        SessionState.SetBool("TpmTest", true); EditorApplication.isPlaying = true;
    }
    static void State(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("TpmTest", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            TutorialFlow.SuppressForTests = true;
            EditorApplication.delayCall += () => GameManager.Instance.StartCoroutine(Guard(Smoke()));
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            foreach (var key in Keys)
                if (SessionState.GetBool("TpmHad" + key, false)) Save.SetInt(key, SessionState.GetInt("TpmVal" + key, 0)); else Save.Delete(key);
            Save.Flush(); TutorialFlow.SuppressForTests = false; SessionState.SetBool("TpmTest", false);
        }
    }
    static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static void Invoke(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, args);
    static void Check(bool pass, string name) { Rows.Add((pass ? "PASS " : "FAIL ") + name); File.WriteAllLines(Root + "checks.txt", Rows); }
    static IEnumerator Guard(IEnumerator run)
    {
        while (true)
        {
            bool more = false; object next = null;
            try { more = run.MoveNext(); if (more) next = run.Current; } catch (Exception e) { Check(false, e.ToString()); }
            if (!more) break; yield return next;
        }
        GameManager.Instance.ResetGame(); Debug.Log("[TutorialPosterMusic] " + Rows.Count(r => r.StartsWith("PASS")) + "/" + Rows.Count + " PASS");
        EditorApplication.isPlaying = false;
    }
    static AudienceMemberActor[] Actors() => UnityEngine.Object.FindObjectsByType<AudienceMemberActor>(FindObjectsSortMode.None).Where(a => a.IsBound && a.gameObject.activeInHierarchy).ToArray();
    static void Stage(string name)
    {
        GameManager.Instance.ResetGame();
        StageRuntimeDirector.Active.ApplyStage(AssetDatabase.LoadAssetAtPath<StageDefinition>("Assets/Settings/Tour/" + name + ".asset"));
        GameManager.Instance.StartGame(); PerformanceTimer.SetPaused(true); AudienceRosterSystem.Instance.SuppressEngagementDecay = true;
    }
    static AudioSource Active() => Field<AudioSource>(AudioManager.Instance, Field<bool>(AudioManager.Instance, "_usingA") ? "_bgmA" : "_bgmB");
    static AudienceMemberActor[] CurrentActors()
    {
        var ids = new HashSet<AudienceId>(AudienceRosterSystem.Instance.Members.Select(m => m.Id));
        return Actors().Where(a => ids.Contains(a.BoundId)).ToArray();
    }
    static IEnumerator Smoke()
    {
        Rows.Clear(); var tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        Stage("Stage01"); tutorial.StartTutorial();
        Invoke(tutorial, "SetRoster", (object)new[] { (CrowdPreference.Chill, 16f), (CrowdPreference.Mosh, 52f), (CrowdPreference.Singalong, 88f) });
        yield return new WaitForSecondsRealtime(.8f);
        var actors = CurrentActors(); var positions = actors.Select(a => Field<Vector3>(a, "_currentLayoutPosition").x).ToArray();
        Check(actors.Length == 3 && Mathf.Abs(positions.Average()) < .001f && positions.All(x => Mathf.Abs(x) <= 1.71f), "current lesson targets centered, excluding fading removed actors");
        var floor = actors.Select(a => a.GroundSortY).ToArray(); yield return new WaitForSecondsRealtime(.5f);
        Check(actors.Select((a, i) => Mathf.Abs(a.GroundSortY - floor[i]) < .001f && Mathf.Abs(Field<Vector3>(a, "_currentLayoutPosition").x - positions[i]) < .001f && Field<Vector2>(a, "_wanderOffset") == Vector2.zero).All(x => x), "fixed floors do not wander or gather; state animation retained");
        Invoke(tutorial, "SetRoster", (object)new[] { (CrowdPreference.Chill, 16f) }); yield return new WaitForSecondsRealtime(.2f);
        Check(CurrentActors().Length == 1 && Mathf.Abs(Field<Vector3>(CurrentActors()[0], "_currentLayoutPosition").x) < .001f && Field<Vector2>(CurrentActors()[0], "_wanderOffset") == Vector2.zero, "single current tutorial target centered");
        tutorial.SkipTutorial(); Check(CurrentActors().All(a => !Field<bool>(a, "_tutorialPlacement")), "ordinary placement restored after tutorial");
    }
    static IEnumerator Run()
    {
        Rows.Clear(); var tutorial = UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        Stage("Stage01"); tutorial.StartTutorial();
        Invoke(tutorial, "SetRoster", (object)new[] { (CrowdPreference.Chill, 16f), (CrowdPreference.Mosh, 52f), (CrowdPreference.Singalong, 88f) });
        yield return new WaitForSeconds(1.5f);
        var actors = Actors(); var floors = actors.ToDictionary(a => a.BoundId, a => a.GroundSortY);
        Check(actors.Length == 3 && Mathf.Abs(actors.Average(a => a.transform.localPosition.x)) < .001f && actors.All(a => Mathf.Abs(a.transform.localPosition.x) <= 1.71f), "tutorial mixed states centered with compact spacing");
        yield return new WaitForSeconds(3f);
        Check(actors.All(a => Mathf.Abs(a.GroundSortY - floors[a.BoundId]) < .001f && Mathf.Abs(a.transform.localPosition.x) <= 1.71f), "Calm does not wander and other states do not gather away during tutorial");
        Check(actors.Select(a => a.SortingOrder).Distinct().Count() == actors.Length, "fixed tutorial retains stable unique depth");
        ScreenCapture.CaptureScreenshot(Root + "tutorial-centered.png"); yield return new WaitForEndOfFrame();
        Invoke(tutorial, "SetRoster", (object)new[] { (CrowdPreference.Chill, 16f) }); yield return new WaitForSeconds(.3f);
        Check(Actors().Length == 1 && Mathf.Abs(Actors()[0].transform.localPosition.x) < .001f, "single tutorial target fixed at exact center");
        tutorial.SkipTutorial(); yield return new WaitForSeconds(.5f);
        Check(Actors().All(a => !Field<bool>(a, "_tutorialPlacement")), "tutorial exit restores ordinary placement flag on pooled actors");
        var roster = AudienceRosterSystem.Instance; roster.SuppressEngagementDecay = true;
        foreach (var m in roster.Members.ToArray()) roster.TrySetEngagement(m.Id, 16, AudienceChangeReason.RuntimeCommand, out _);
        yield return new WaitForSeconds(8f);
        Check(Actors().Any(a => Mathf.Abs(a.transform.localPosition.x) > 3f), "ordinary Calm resumes outer-map wandering after tutorial");
        Stage("Stage02"); tutorial.StartTutorial(); yield return new WaitForSeconds(1f);
        Check(TutorialFlow.IsRunning && Actors().All(a => Field<bool>(a, "_tutorialPlacement")), "Stage2 special lesson also fixes normal audience centrally");
        Check(Bgm.Current == "big_rock", "Stage2 tutorial keeps Big Rock");
        tutorial.SkipTutorial();
        Stage("Stage03"); yield return new WaitForSeconds(1.8f);
        Check(Bgm.Current == "dogtown" && Active().clip.name == "Dogtown - National Sweetheart" && Active().isPlaying, "Stage3 actual Dogtown AudioSource plays");
        Stage("Stage05Boss"); yield return new WaitForSeconds(1.8f);
        var boss = UnityEngine.Object.FindObjectsByType<BossBattleRule>(FindObjectsSortMode.None).First(b => b.IsActive);
        Check(boss.RivalFans > boss.OurFans && Bgm.Current == "360_no_scope", "initial boss majority uses 360 No Scope");
        var old = Active(); boss.StealFans((boss.RivalFans - boss.OurFans) / 2 + 1);
        var incoming = Active(); yield return new WaitForSeconds(.35f);
        Check(boss.OurFans > boss.RivalFans && Bgm.Current == "pomade" && incoming.clip.name == "Pomade - Silent Partner", "real fan recruitment majority changes to Pomade");
        Check(old != incoming && old.isPlaying && incoming.isPlaying && old.volume > 0 && incoming.volume > 0, "both sources crossfade rather than abrupt replacement");
        yield return new WaitForSeconds(1.4f);
        Check(!old.isPlaying && incoming.isPlaying && incoming.volume > 0, "crossfade completes and old source stops");
        float time = incoming.time; boss.StealFans(1); yield return new WaitForSeconds(.15f);
        Check(Active() == incoming && incoming.time > time, "same majority does not restart track");
        Invoke(boss, "LoseOurFans", (boss.OurFans - boss.RivalFans) / 2); yield return null;
        Check(boss.OurFans == boss.RivalFans && Bgm.Current == "pomade", "tie retains player-leading song");
        Invoke(boss, "LoseOurFans", 1); yield return new WaitForSeconds(1.8f);
        Check(boss.RivalFans > boss.OurFans && Bgm.Current == "360_no_scope" && Active().isPlaying, "boss regains majority and fades back");
        boss.StealFans(1); yield return null;
        Check(boss.OurFans == boss.RivalFans && Bgm.Current == "360_no_scope", "tie retains boss-leading song");
        boss.StealFans(1); yield return new WaitForSeconds(1.8f);
        Check(Bgm.Current == "pomade", "second player majority switches again");
        Stage("Stage03"); yield return new WaitForSeconds(1.8f);
        Check(Bgm.Current == "dogtown", "leaving boss resets dynamic selection");
        Stage("Stage05Boss"); yield return new WaitForSeconds(1.8f);
        Check(Bgm.Current == "360_no_scope", "boss replay starts from fresh majority");

    }
    static IEnumerator Posters()
    {
        DialoguePanel panel = null;
        var catalog = StageVisualCatalog.LoadDefault();
        foreach (string id in new[] { "stage_01", "stage_02", "stage_04" })
        {
            catalog.TryGet(id, out var entry); bool completed = false;
            Dialogue.TryPlay("__poster_smoke__", new DialoguePresentationContext { ruleTitle = entry.ruleTitle, ruleBody = entry.ruleBody, stagePoster = entry.stagePoster, posterPresentation = entry.posterPresentation }, () => completed = true);
            panel = UnityEngine.Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
            yield return new WaitForSeconds(.6f); Canvas.ForceUpdateCanvases();
            var root = Field<GameObject>(panel, "ruleCardRoot");
            var confirm = Field<Button>(panel, "ruleConfirmButton"); var label = Field<TMP_Text>(panel, "ruleConfirmLabel");
            Check(panel.DebugState == "RuleCard" && !root.GetComponent<Image>().enabled, id + " real poster replaces grey panel");
            if (id == "stage_01")
            {
                var image = root.transform.Find("PosterHeadlineLogo").GetComponent<Image>();
                Check(image.gameObject.activeInHierarchy && AssetDatabase.GetAssetPath(image.sprite).EndsWith("Stage01_HandwrittenTitle_v1.png") && !Field<TMP_Text>(panel, "ruleTitleText").gameObject.activeSelf, "Stage1 existing handwritten PNG replaces text headline");
            }
            if (id == "stage_02")
            {
                var rect = (RectTransform)confirm.transform;
                Check(rect.anchorMin.x >= .74f && rect.anchorMax.x <= .98f && rect.anchorMin.y >= .04f && rect.anchorMax.y <= .19f && label.text == "공연\n시작" && !label.isTextOverflowing, "Stage2 compact entry fits marked lower-right paper");
            }
            if (id == "stage_04")
            {
                var title = Field<TMP_Text>(panel, "ruleTitleText"); title.ForceMeshUpdate();
                Check(title.text == "Raccoon Roll\nON AIR!" && !title.isTextOverflowing, "Stage4 new English headline fits");
            }
            ScreenCapture.CaptureScreenshot(Root + id + "-poster.png"); yield return new WaitForEndOfFrame();
            var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)confirm.transform).TransformPoint(((RectTransform)confirm.transform).rect.center)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Check(hits.Any(h => h.gameObject == confirm.gameObject || h.gameObject.transform.IsChildOf(confirm.transform)), id + " relocated button remains raycast target");
            ExecuteEvents.Execute(confirm.gameObject, data, ExecuteEvents.pointerClickHandler);
            yield return new WaitForSeconds(.5f);
            Check(completed, id + " actual Button pointer-click completes poster");
        }
    }
}
