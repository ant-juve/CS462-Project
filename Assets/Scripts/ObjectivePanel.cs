using System.Collections;
using System.Collections.Generic;
using Core.Gameplay.Objectives;

using UnityEngine;

namespace UI.Game.Objectives
{
    public class ObjectivePanel : MonoBehaviour
    {
        [SerializeField]
        private ObjectiveDisplay _objectiveDisplayPrefab;
        
        [SerializeField]
        private Transform _objectiveDisplayParent;

        [SerializeField] private ObjectiveManager _objectiveManager;
        
        private readonly List<ObjectiveDisplay> _listDisplay = new();

        void Start()
        {
            StartCoroutine(DelayedInit());
        }
        
      private IEnumerator DelayedInit()
{
    while (_objectiveManager == null)
    {
        yield return null;
    }

    foreach (var objective in _objectiveManager.Objectives)
    {
        AddObjective(objective);
    }

    _objectiveManager.OnObjectiveAdded += AddObjective;
}

        private void AddObjective(Objective obj)
        {
            var display = Instantiate(_objectiveDisplayPrefab, _objectiveDisplayParent);
            display.Init(obj);
            Debug.Log($"Objective row initialized: {obj.GetStatusText()}", display.gameObject);
            _listDisplay.Add(display);
        }

        public void Reset()
        {
            for (var i = _listDisplay.Count - 1; i >= 0; i--)
            {
                Destroy(_listDisplay[i].gameObject);
            }
            _listDisplay.Clear();
        }
    }
}