using System;
using System.Collections.Generic;
using System.Linq;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// SlotManager 内部で Slot を管理する Registry。
    /// <para>
    /// <c>validation-design.md [N-3]</c> 対応として <c>internal sealed class</c> で定義し、
    /// パッケージ外からの直接参照を防ぐ。テストからの参照は <c>InternalsVisibleTo</c> で許可する。
    /// </para>
    /// </summary>
    internal sealed class SlotRegistry
    {
        private readonly Dictionary<string, SlotHandle> _slots = new Dictionary<string, SlotHandle>();

        /// <summary>
        /// 新規 Slot を登録する。初期状態は <see cref="SlotState.Created"/>。
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 同一 <paramref name="slotId"/> が既に登録されている場合 (Req 2.3)。
        /// </exception>
        public void AddSlot(string slotId, SlotSettings settings)
        {
            if (_slots.ContainsKey(slotId))
                throw new InvalidOperationException(
                    $"slotId '{slotId}' は既に登録されています。重複登録はサポートされません。");

            var handle = new SlotHandle(slotId, settings.displayName, SlotState.Created, settings);
            _slots.Add(slotId, handle);
        }

        /// <summary>
        /// 指定 Slot を登録から除去する。
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 指定 <paramref name="slotId"/> が登録されていない場合。
        /// </exception>
        public void RemoveSlot(string slotId)
        {
            if (!_slots.Remove(slotId))
                throw new InvalidOperationException(
                    $"slotId '{slotId}' は登録されていないため削除できません。");
        }

        /// <summary>
        /// 指定 <paramref name="slotId"/> の <see cref="SlotHandle"/> を返す。存在しない場合は null。
        /// </summary>
        public SlotHandle GetSlot(string slotId)
        {
            return _slots.TryGetValue(slotId, out var handle) ? handle : null;
        }

        /// <summary>
        /// 登録済み Slot の一覧を返す (Req 2.4)。
        /// </summary>
        public IReadOnlyList<SlotHandle> GetAllSlots()
        {
            return _slots.Values.ToList().AsReadOnly();
        }
    }
}
