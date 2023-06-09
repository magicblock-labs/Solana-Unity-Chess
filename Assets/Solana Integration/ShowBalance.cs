using System;
using Cysharp.Threading.Tasks;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using TMPro;
using UnityEngine;

// ReSharper disable once CheckNamespace

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
    
    private void BalanceChanged(double balance)
    {
        _txtBalance.text = $"Balance: {Math.Round(balance, 3)} SOL";
        
        // Try to request airdrop if balance is 0
        if (Web3.Account != null && balance == 0) RequestAirdrop().Forget();
    }
    
    private async UniTask RequestAirdrop()
    { 
        await Web3.Wallet.RequestAirdrop(commitment: Commitment.Confirmed);
    }
}
