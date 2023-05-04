using Solana.Unity.SDK;
using TMPro;
using UnityEngine;

public class ShowBalance : MonoBehaviour
{
    private TextMeshProUGUI _txtBalance;

    private void Awake()
    {
        _txtBalance = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
       Web3.OnBalanceChange += BalanceChanged;
    }

    private void OnDisable()
    {
        Web3.OnBalanceChange -= BalanceChanged;
    }
    
    private void BalanceChanged(double sol)
    {
        _txtBalance.text = $"{sol} SOL";
    }
}
