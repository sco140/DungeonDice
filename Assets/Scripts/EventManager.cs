using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameUtility
{
    struct EventNames
    {

    }
    public abstract class EventHandlerBase
    {
        abstract public void Invoke();
    }
    public class EventHandler : EventHandlerBase
    {
        private readonly Action m_delegate;
        public EventHandler(Action newFunc)
        {
            m_delegate = newFunc;
        }
        override public void Invoke()
        {
            m_delegate.Invoke();
        }
    }
    public class EventHandler<I> : EventHandlerBase
    {
        private readonly Action<I> m_delegate;
        private I m_paramI;
        public EventHandler(Action<I> newFunc, I paramI)
        {
            m_delegate = newFunc;
            m_paramI = paramI;
        }
        override public void Invoke()
        {
            m_delegate.Invoke(m_paramI);
        }
    }
    public class EventHandler<I, J> : EventHandlerBase
    {
        private readonly Action<I, J> m_delegate;
        private readonly I m_paramI;
        private readonly J m_paramJ;
        public EventHandler(Action<I, J> newFunc, I paramI, J paramJ)
        {
            m_delegate = newFunc;
            m_paramI = paramI;
            m_paramJ = paramJ;
        }
        override public void Invoke()
        {
            m_delegate.Invoke(m_paramI, m_paramJ);
        }
    }
    public class EventHandler<I, J, K> : EventHandlerBase
    {
        private readonly Action<I, J, K> m_delegate;
        private readonly I m_paramI;
        private readonly J m_paramJ;
        private readonly K m_paramK;
        public EventHandler(Action<I, J, K> newFunc, I paramI, J paramJ, K paramK)
        {
            m_delegate = newFunc;

            m_paramI = paramI;
            m_paramJ = paramJ;
            m_paramK = paramK;
        }
        override public void Invoke()
        {
            m_delegate.Invoke(m_paramI, m_paramJ, m_paramK);
        }
    }
    public class EventManager : MonoBehaviour
    {
        private Dictionary<string, List<EventHandlerBase>> m_dEventDictionary = new();
        public static EventManager Instance { get; private set; }
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }
        public void Register(string eventName, EventHandlerBase listener)
        {
            if (!m_dEventDictionary.ContainsKey(eventName))
            {
                m_dEventDictionary[eventName] = new List<EventHandlerBase>();
            }
            m_dEventDictionary[eventName].Add(listener);
        }
        public void Unregister(string eventName, EventHandlerBase listener)
        {
            if (m_dEventDictionary.TryGetValue(eventName, out List<EventHandlerBase> listeners))
            {
                listeners.Remove(listener);
                if (listeners.Count == 0)
                {
                    m_dEventDictionary.Remove(eventName);
                }
            }
        }
        public void TriggerEvent(string eventName)
        {
            StartCoroutine(HandleEvent(eventName));
        }
        private IEnumerator HandleEvent(string eventName)
        {
            if (m_dEventDictionary.TryGetValue(eventName, out List<EventHandlerBase> listeners))
            {
                foreach (EventHandlerBase listener in listeners)
                {
                    listener.Invoke();
                }
            }
            yield return null;
        }
    }
}