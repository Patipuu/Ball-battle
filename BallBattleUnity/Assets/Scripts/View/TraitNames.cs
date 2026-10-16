using BallBattle.Sim.Traits;
using System.Text.RegularExpressions;

namespace BallBattle.View
{
    /// <summary>Names shown to players; trait ids and simulation data stay language neutral.</summary>
    public static class TraitNames
    {
        public static string Of(string traitId)
        {
            var english = TraitRegistry.Get(traitId).DisplayName;
            if (StringTable.Current != Lang.Vi) return english;
            return traitId == HeavyTrait.TraitId ? "NẶNG KÝ" : UiVietnamese.Translate(english);
        }

        /// <summary>Localize an older plain-text trait list when structured trait entries are unavailable.</summary>
        public static string List(string english)
        {
            if (StringTable.Current != Lang.Vi || string.IsNullOrEmpty(english)) return english;
            foreach (var trait in TraitRegistry.All)
                english = Regex.Replace(english,
                    @"(?<!\S)" + Regex.Escape(trait.DisplayName) + @"(?=\s|$)", Of(trait.Id), RegexOptions.CultureInvariant);
            return english;
        }
    }
}
