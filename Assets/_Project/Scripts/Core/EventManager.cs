using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameUtility
{
    public abstract class EventHandlerBase
    {
    }
    public class EventHandler : EventHandlerBase
    {
        private readonly Action m_delegate;
        public Action GetDelegate() => m_delegate;
        public EventHandler(Action newFunc)
        {
            m_delegate = newFunc;
        }
        public void Invoke()
        {
            m_delegate.Invoke();
        }
    }
    public class EventHandler<I> : EventHandlerBase
    {
        private readonly Action<I> m_delegate;
        public Action<I> GetDelegate() => m_delegate;
        public EventHandler(Action<I> newFunc)
        {
            m_delegate = newFunc;
        }
        public void Invoke(I paramI)
        {
            m_delegate.Invoke(paramI);
        }
        public void ReferenceInvoke(ref I paramI)
        {
            m_delegate.Invoke(paramI);
        }
    }
    public class EventHandler<I, J> : EventHandlerBase
    {
        private readonly Action<I, J> m_delegate;
        public Action<I, J> GetDelegate() => m_delegate;
        public EventHandler(Action<I, J> newFunc)
        {
            m_delegate = newFunc;
        }
        public void Invoke(I paramI, J paramJ)
        {
            m_delegate.Invoke(paramI, paramJ);
        }
        public void ReferenceInvoke(ref I paramI, ref J paramJ)
        {
            m_delegate.Invoke(paramI, paramJ);
        }
    }
    public class EventHandler<I, J, K> : EventHandlerBase
    {
        private readonly Action<I, J, K> m_delegate;
        public Action<I, J, K> GetDelegate() => m_delegate;
        public EventHandler(Action<I, J, K> newFunc)
        {
            m_delegate = newFunc;
        }
        public void Invoke(I paramI, J paramJ, K paramK)
        {
            m_delegate.Invoke(paramI, paramJ, paramK);
        }
        public void ReferenceInvoke(ref I paramI, ref J paramJ, ref K paramK)
        {
            m_delegate.Invoke(paramI, paramJ, paramK);
        }
    }
    public class EventManager : MonoBehaviour
    {
        private Dictionary<string, List<EventHandlerBase>> m_dEventDictionary = new();
        public static EventManager Instance { get; private set; }
        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        public static int Register(string eventName, EventHandlerBase listener)
        {
            if (!Instance.m_dEventDictionary.ContainsKey(eventName))
            {
                Instance.m_dEventDictionary[eventName] = new List<EventHandlerBase>();
            }
            Instance.m_dEventDictionary[eventName].Add(listener);

            // Index of the newly added listener for potential future reference or removal.
            return Instance.m_dEventDictionary[eventName].Count - 1;
        }
        public static int Register<I>(string eventName, EventHandler<I> listener)
        {
            if (!Instance.m_dEventDictionary.ContainsKey(eventName))
            {
                Instance.m_dEventDictionary[eventName] = new List<EventHandlerBase>();
            }
            Instance.m_dEventDictionary[eventName].Add(listener);

            // Index of the newly added listener for potential future reference or removal.
            return Instance.m_dEventDictionary[eventName].Count - 1;
        }
        public static int Register<I, J>(string eventName, EventHandler<I, J> listener)
        {
            if (!Instance.m_dEventDictionary.ContainsKey(eventName))
            {
                Instance.m_dEventDictionary[eventName] = new List<EventHandlerBase>();
            }
            Instance.m_dEventDictionary[eventName].Add(listener);
            // Index of the newly added listener for potential future reference or removal.
            return Instance.m_dEventDictionary[eventName].Count - 1;
        }
        public static int Register<I, J, K>(string eventName, EventHandler<I, J, K> listener)
        {
            if (!Instance.m_dEventDictionary.ContainsKey(eventName))
            {
                Instance.m_dEventDictionary[eventName] = new List<EventHandlerBase>();
            }
            Instance.m_dEventDictionary[eventName].Add(listener);
            // Index of the newly added listener for potential future reference or removal.
            return Instance.m_dEventDictionary[eventName].Count - 1;
        }
        public static void Unregister(string eventName, Action function)
        {
            if (function == null)
            {
                return;
            }

            ref var eventDictionary = ref Instance.m_dEventDictionary;
            if (!eventDictionary.TryGetValue(
                eventName,
                out List<EventHandlerBase> listeners))
            {
                return;
            }

            if (listeners.Count == 0)
            {
                eventDictionary.Remove(eventName);
            }

            /// Remove all listeners that match the provided function.
            listeners.Select(x => x as EventHandler)
                     .Where(x => x != null && x.GetDelegate() == function)
                     .ToList()
                     .ForEach(x => listeners.Remove(x));
        }
        public static void Unregister<I>(string eventName, Action<I> function)
        {
            if (function == null)
            {
                return;
            }
            ref var eventDictionary = ref Instance.m_dEventDictionary;
            if (!eventDictionary.TryGetValue(
                eventName,
                out List<EventHandlerBase> listeners))
            {
                return;
            }
            if (listeners.Count == 0)
            {
                eventDictionary.Remove(eventName);
            }

            /// Remove all listeners that match the provided function.
            listeners.Select(x => x as EventHandler<I>)
                     .Where(x => x != null && x.GetDelegate() == function)
                     .ToList()
                     .ForEach(x => listeners.Remove(x));
        }

        public static void Unregister<I, J>(string eventName, Action<I, J> function)
        {
            if (function == null)
            {
                return;
            }
            ref var eventDictionary = ref Instance.m_dEventDictionary;
            if (!eventDictionary.TryGetValue(
                eventName,
                out List<EventHandlerBase> listeners))
            {
                return;
            }
            if (listeners.Count == 0)
            {
                eventDictionary.Remove(eventName);
            }
            /// Remove all listeners that match the provided function.
            listeners.Select(x => x as EventHandler<I, J>)
                     .Where(x => x != null && x.GetDelegate() == function)
                     .ToList()
                     .ForEach(x => listeners.Remove(x));
        }

        public static void Unregister<I, J, K>(string eventName, Action<I, J, K> function)
        {
            if (function == null)
            {
                return;
            }
            ref var eventDictionary = ref Instance.m_dEventDictionary;
            if (!eventDictionary.TryGetValue(
                eventName,
                out List<EventHandlerBase> listeners))
            {
                return;
            }
            if (listeners.Count == 0)
            {
                eventDictionary.Remove(eventName);
            }
            /// Remove all listeners that match the provided function.
            listeners.Select(x => x as EventHandler<I, J, K>)
                     .Where(x => x != null && x.GetDelegate() == function)
                     .ToList()
                     .ForEach(x => listeners.Remove(x));
        }

        public static void TriggerEvent(string eventName)
        {
            Instance.StartCoroutine(Instance.HandleEvent(eventName));
        }
        public static void TriggerEvent<I>(string eventName, I paramI)
        {
            Instance.StartCoroutine(Instance.HandleEvent(eventName, paramI));
        }
        public static void TriggerEvent<I, J>(string eventName, I paramI, J paramJ)
        {
            Instance.StartCoroutine(Instance.HandleEvent(eventName, paramI, paramJ));
        }
        public static void TriggerEvent<I, J, K>(string eventName, I paramI, J paramJ, K paramK)
        {
            Instance.StartCoroutine(Instance.HandleEvent(eventName, paramI, paramJ, paramK));
        }
        private IEnumerator HandleEvent(string eventName)
        {
            if (m_dEventDictionary.TryGetValue(eventName, out List<EventHandlerBase> listeners))
            {
                foreach (EventHandlerBase listener in listeners)
                {
                    ((EventHandler)listener).Invoke();
                }
            }
            else
            {
                Debug.LogWarning($"Event '{eventName}' was never registered.");
            }
            yield return null;
        }
        private IEnumerator HandleEvent<I>(string eventName, I paramI)
        {
            if (m_dEventDictionary.TryGetValue(eventName, out List<EventHandlerBase> listeners))
            {
                foreach (EventHandlerBase listener in listeners)
                {
                    ((EventHandler<I>)listener).Invoke(paramI);
                }
            }
            else
            {
                Debug.LogWarning($"Event '{eventName}' was never registered.");
            }
            yield return null;
        }
        private IEnumerator HandleEvent<I, J>(string eventName, I paramI, J paramJ)
        {
            if (m_dEventDictionary.TryGetValue(eventName, out List<EventHandlerBase> listeners))
            {
                foreach (EventHandlerBase listener in listeners)
                {
                    ((EventHandler<I, J>)listener).Invoke(paramI, paramJ);
                }
            }
            else
            {
                Debug.LogWarning($"Event '{eventName}' was never registered.");
            }
            yield return null;
        }
        private IEnumerator HandleEvent<I, J, K>(string eventName, I paramI, J paramJ, K paramK)
        {
            if (m_dEventDictionary.TryGetValue(eventName, out List<EventHandlerBase> listeners))
            {
                foreach (EventHandlerBase listener in listeners)
                {
                    ((EventHandler<I, J, K>)listener).Invoke(paramI, paramJ, paramK);
                }
            }
            else
            {
                Debug.LogWarning($"Event '{eventName}' was never registered.");
            }
            yield return null;
        }

    }
}