using System;
using UniRx;
using UnityEngine;

namespace RealtimeAvatarController.Core
{
    /// <summary>
    /// <see cref="ISlotErrorChannel"/> のデフォルト実装。
    /// Subject&lt;SlotError&gt;.Synchronize() によりスレッドセーフな発行を保証する。
    /// Debug.LogError の出力は同一 (SlotId, Category) 組合せにつき初回のみ行い、以降は抑制する。
    /// </summary>
    internal sealed class DefaultSlotErrorChannel : ISlotErrorChannel
    {
        private readonly ISubject<SlotError> _subject = new Subject<SlotError>().Synchronize();

        /// <inheritdoc/>
        public IObservable<SlotError> Errors => _subject.AsObservable();

        /// <inheritdoc/>
        public void Publish(SlotError error)
        {
            _subject.OnNext(error);

            var key = (error.SlotId, error.Category);
            if (RegistryLocator.s_suppressedErrors.Add(key))
            {
                Debug.LogError($"[SlotError] SlotId={error.SlotId}, Category={error.Category}, Exception={error.Exception}");
            }
        }
    }
}
