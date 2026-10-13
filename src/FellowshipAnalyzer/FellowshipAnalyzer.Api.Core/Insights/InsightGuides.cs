using FellowshipAnalyzer.Api.Core.Insights.Prompts;
using FellowshipAnalyzer.Core.Insights.Aeona;
using FellowshipAnalyzer.Core.Insights.Ardeos;

namespace FellowshipAnalyzer.Api.Core.Insights;

/// <summary>Every guide with insights.</summary>
public static class InsightGuides
{
    /// <summary>Every guide with insights, by hero.</summary>
    public static IReadOnlyList<InsightGuide> All { get; } = [.. Aeona(AeonaDigestJsonContext.Default), .. Ardeos(ArdeosDigestJsonContext.Default)];

    /// <summary>The guide <paramref name="guide"/> of <paramref name="hero"/>, or null when it has no insights.</summary>
    public static InsightGuide? Find(string hero, string guide) =>
        All.FirstOrDefault(entry => string.Equals(entry.Hero, hero, StringComparison.OrdinalIgnoreCase) && entry.Guide == guide);

    private static IEnumerable<InsightGuide> Aeona(AeonaDigestJsonContext json) =>
    [
        new InsightGuide<EntropyClaimPull>(AeonaGuides.Hero, AeonaGuides.EntropyClaim, AeonaPrompts.Overview, AeonaPrompts.EntropyClaim, AeonaDigestValues.All, json.InsightMessageEntropyClaimPull),
        new InsightGuide<OblivionPull>(AeonaGuides.Hero, AeonaGuides.Oblivion, AeonaPrompts.Overview, AeonaPrompts.Oblivion, AeonaDigestValues.All, json.InsightMessageOblivionPull),
        new InsightGuide<StaggerCleansePull>(AeonaGuides.Hero, AeonaGuides.StaggerCleanse, AeonaPrompts.Overview, AeonaPrompts.StaggerCleanse, AeonaDigestValues.All, json.InsightMessageStaggerCleansePull),
        new InsightGuide<ContinuumShiftPull>(AeonaGuides.Hero, AeonaGuides.ContinuumShift, AeonaPrompts.Overview, AeonaPrompts.ContinuumShift, AeonaDigestValues.All, json.InsightMessageContinuumShiftPull),
        new InsightGuide<KindRewindPull>(AeonaGuides.Hero, AeonaGuides.KindRewind, AeonaPrompts.Overview, AeonaPrompts.KindRewind, AeonaDigestValues.All, json.InsightMessageKindRewindPull),
    ];

    private static IEnumerable<InsightGuide> Ardeos(ArdeosDigestJsonContext json) =>
    [
        new InsightGuide<WildfirePull>(ArdeosGuides.Hero, ArdeosGuides.WildfireCombo, ArdeosPrompts.Overview, ArdeosPrompts.WildfireCombo, [], json.InsightMessageWildfirePull),
        new InsightGuide<EngulfingFlamesPull>(ArdeosGuides.Hero, ArdeosGuides.EngulfingFlamesEconomy, ArdeosPrompts.Overview, ArdeosPrompts.EngulfingFlamesEconomy, [], json.InsightMessageEngulfingFlamesPull),
        new InsightGuide<DetonatePull>(ArdeosGuides.Hero, ArdeosGuides.DetonateEfficiency, ArdeosPrompts.Overview, ArdeosPrompts.DetonateEfficiency, [], json.InsightMessageDetonatePull),
        new InsightGuide<SearingBlazePull>(ArdeosGuides.Hero, ArdeosGuides.SearingBlaze, ArdeosPrompts.Overview, ArdeosPrompts.SearingBlaze, [], json.InsightMessageSearingBlazePull),
    ];
}
