using System;
using UnityEngine;

namespace EasyInput
{
    // Copy-on-write subscriptions: stable dispatch, isolated callbacks, no allocations when raising an event.
    internal sealed class InputSignal
    {
        private Action[] handlers = Array.Empty<Action>();
        public void Add(Action handler)
        {
            if (handler == null) return;
            var next = new Action[handlers.Length + 1]; Array.Copy(handlers, next, handlers.Length); next[handlers.Length] = handler; handlers = next;
        }
        public void Remove(Action handler)
        {
            int index = Array.LastIndexOf(handlers, handler); if (index < 0) return;
            var next = new Action[handlers.Length - 1]; Array.Copy(handlers, 0, next, 0, index); Array.Copy(handlers, index + 1, next, index, next.Length - index); handlers = next;
        }
        public void Raise()
        {
            var snapshot = handlers;
            for (int i = 0; i < snapshot.Length; ++i) try { snapshot[i](); } catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
