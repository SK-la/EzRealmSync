#if HAS_EZ_OSU_GAME
using osu.Game.EzOsuGame.ScriptedSkin;
using osu.Game.Extensions;
using osu.Game.Skinning;

namespace osu.Game.EzRealmSync.Realm
{
    /// <summary>
    /// 转官方时排除 Ez 代码皮肤与脚本衍生皮肤。
    /// </summary>
    public static class OfficialConvertSkinFilter
    {
        private static readonly string ez2_skin_instantiation = typeof(Ez2Skin).GetInvariantInstantiationInfo();
        private static readonly string ez_style_pro_skin_instantiation = typeof(EzStyleProSkin).GetInvariantInstantiationInfo();
        private static readonly string sbi_skin_instantiation = typeof(SbISkin).GetInvariantInstantiationInfo();

        private static readonly Guid ez2_skin_id = new Guid("fc372386-381d-4f8e-897a-c1d89ef39f9c");
        private static readonly Guid ez_style_pro_skin_id = new Guid("1E70839C-C0D8-4DBF-B747-0C08C89D412B");
        private static readonly Guid sbi_skin_id = new Guid("fc372386-381d-4f8e-897a-c1d89ef39f2c");

        public static bool ShouldExcludeFromOfficial(SkinInfo skin) =>
            skin.DeletePending
            || isEzBuiltinCodeSkin(skin)
            || ScriptedSkinSupport.IsScriptedSkin(skin);

        /// <summary>与 <see cref="ShouldExcludeFromOfficial"/> 相反，用于迁移后写回辅助表。</summary>
        public static bool IsEzOnlyProtectedSkin(SkinInfo skin) => isEzBuiltinCodeSkin(skin) || ScriptedSkinSupport.IsScriptedSkin(skin);

        private static bool isEzBuiltinCodeSkin(SkinInfo skin) =>
            skin.ID == ez2_skin_id
            || skin.ID == ez_style_pro_skin_id
            || skin.ID == sbi_skin_id
            || string.Equals(skin.InstantiationInfo, ez2_skin_instantiation, StringComparison.Ordinal)
            || string.Equals(skin.InstantiationInfo, ez_style_pro_skin_instantiation, StringComparison.Ordinal)
            || string.Equals(skin.InstantiationInfo, sbi_skin_instantiation, StringComparison.Ordinal);
    }
}
#endif
