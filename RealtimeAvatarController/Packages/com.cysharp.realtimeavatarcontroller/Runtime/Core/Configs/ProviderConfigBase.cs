using UnityEngine;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// IAvatarProvider 用 Config の抽象基底クラス。
    /// 具象 Config (例: BuiltinAvatarProviderConfig) はこのクラスを継承して定義する。
    /// Inspector でのドラッグ&amp;ドロップによる型安全参照を実現しつつ、
    /// 具象型依存を Factory 側のキャストに閉じ込める。
    /// [CreateAssetMenu] は具象クラスが付与する設計のため、本クラスには付与しない。
    /// </summary>
    public abstract class ProviderConfigBase : ScriptableObject { }
}
