using UnityEngine;
using KSP;
// Fix: removed stale using SyndicateMangaer (typo namespace, now unified into Koko)

// Added namespace. Was in global namespace.
namespace Koko
{
[KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION)]
public class SyndicateScenario : ScenarioModule
{
    public override void OnLoad(ConfigNode node)
    {
        SyndicateManager.LoadFromSave(node);  // delegate
    }

    public override void OnSave(ConfigNode node)
    {
        SyndicateManager.SaveToSave(node);    // delegate
    }
}
} // namespace Koko
