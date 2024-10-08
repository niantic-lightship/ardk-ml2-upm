using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NumpadPanel : MonoBehaviour
{
    [SerializeField]
    private Button[] _numberButtonsInOrder;

    [SerializeField]
    private Button _backspaceButton;

    [SerializeField]
    private Text _currentNumberText;

    private UnityAction[] _numberButtonActions; // Keep track of delegates to remove them on disable
    private int _maxNumberLength = 4;
    public int MaxNumberLength
    {
        get => _maxNumberLength;
        set => _maxNumberLength = value;
    }

    private string _currentNumber = "";
    public string CurrentNumber => _currentNumber;

    public event Action OnNumberEntered;

    private void OnEnable()
    {
        _numberButtonActions = new UnityAction[_numberButtonsInOrder.Length];

        for (int i = 0; i < _numberButtonsInOrder.Length; i++)
        {
            var number = i;
            _numberButtonActions[i] = () => OnNumberButtonClicked(number);
            _numberButtonsInOrder[i].onClick.AddListener(_numberButtonActions[i]);
        }

        _backspaceButton.onClick.AddListener(OnBackspaceButtonClicked);
    }

    private void OnDisable()
    {
        for (int i = 0; i < _numberButtonsInOrder.Length; i++)
        {
            _numberButtonsInOrder[i].onClick.RemoveListener(_numberButtonActions[i]);
        }

        _backspaceButton.onClick.RemoveListener(OnBackspaceButtonClicked);
    }

    private void OnNumberButtonClicked(int number)
    {
        if (_currentNumber.Length >= _maxNumberLength)
        {
            return;
        }
        _currentNumber += number.ToString();
        _currentNumberText.text = _currentNumber;
        OnNumberEntered?.Invoke();
    }

    private void OnBackspaceButtonClicked()
    {
        if (_currentNumber.Length > 0)
        {
            _currentNumber = _currentNumber.Substring(0, _currentNumber.Length - 1);
            _currentNumberText.text = _currentNumber;
        }
        OnNumberEntered?.Invoke();
    }
}
