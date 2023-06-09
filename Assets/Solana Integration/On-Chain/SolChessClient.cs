using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using codebase.utility;
using Cysharp.Threading.Tasks;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using SolChess.Program;
using SolChess.Types;
using TMPro;
using UnityChess;
using UnityEngine;
using UnityEngine.UI;
using Color = SolChess.Types.Color;
using Game = SolChess.Accounts.Game;
using Piece = UnityChess.Piece;
using Square = UnityChess.Square;

//ReSharper disable once CheckNamespace

public class SolChessClient : MonoBehaviour
{
    [SerializeField]
    private Button newGameBtn;
    
    [SerializeField]
    private Button joinGameBtn;
    
    [SerializeField]
    private TextMeshProUGUI txtGameId;
    
    //private readonly PublicKey solchessProgramId = new("ChessfTT9XpWA9WxrSSLMnCseRqykb9LaMXKMhyWEiR4");
    private readonly PublicKey _solchessProgramId = new("CCdU3zmYqPZaR2twy5hqcJmFV36tRpFC81seKUE8HVwX");
    
    private SolChess.SolChessClient _solChessProgramClient;
    private PublicKey _gameInstanceId;
    private Toast _toast;

    private SolChess.SolChessClient SolChessProgramClient => _solChessProgramClient ??= 
        new SolChess.SolChessClient(Web3.Rpc, Web3.WsRpc, _solchessProgramId);
    
    void Start()
    {
        joinGameBtn.onClick.AddListener(CallJoinGame);
        newGameBtn.onClick.AddListener(CallNewGame);
        _toast = GetComponent<Toast>();
    }

    private void CallJoinGame()
    {
        JoinGame().Forget();
    }
    
    private void CallNewGame()
    {
        NewGame().Forget();
    }

    private void OnEnable()
    {
        GameManager.MoveEvent += OnMove;
    }
    
    private void OnDisable()
    {
        GameManager.MoveEvent -= OnMove;
    }

    private async UniTask SubscribeToGame(PublicKey gameId)
    {
        await _solChessProgramClient.SubscribeGameAsync(gameId, OnGameUpdate, Commitment.Confirmed);
    }

    private void OnGameUpdate(SubscriptionState subState, ResponseValue<AccountInfo> gameInfo, Game game)
    {
        Debug.Log("Game updated");
        if (GameState.White.Equals(game.GameState) || GameState.Black.Equals(game.GameState))
        {
            newGameBtn.gameObject.transform.parent.parent.gameObject.SetActive(false);
        }
        SetGame(game);
    }

    private async UniTask NewGame()
    {
        if(Web3.Account == null) return;
        Loading.StartLoading();
        var userPda = FindUserPda(Web3.Account);
        ulong gamePdaIdx = 0;
        PublicKey gamePda = null;
        Debug.Log($"Searching game PDA");
        while (gamePda == null)
        {
            var gameTempPda = FindGamePda(userPda, gamePdaIdx);
            if(!await IsPdaInitialized(gameTempPda)) gamePda = gameTempPda;
            gamePdaIdx++;
        }
        Debug.Log($"Sending transaction new Game");
        var res = await JoinGameTransaction(gamePda, Color.White, true);
        Debug.Log($"Signature: {res.Result}");
        if (res.WasSuccessful)
        {
            Debug.Log($"Before confirm");
            await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
            Debug.Log($"After confirm");
            
            Game game = null;
            var retry = 5;
            while (game == null && retry > 0)
            {
                game = (await SolChessProgramClient.GetGameAsync(gamePda, Commitment.Confirmed)).ParsedResult;
                retry--;
                await UniTask.Delay(TimeSpan.FromSeconds(1));
            }
            Debug.Log($"Game retrieved");
            _gameInstanceId = gamePda;
            Debug.Log($"Setting game");
            SetGame(game);
            Debug.Log($"Subcribing to game");
            SubscribeToGame(gamePda).Forget();
            Debug.Log($"Game Id: {gamePda}");
            newGameBtn.onClick.RemoveAllListeners();
            newGameBtn.onClick.AddListener(() =>
            {
                Clipboard.Copy(gamePda.ToString());
                _toast.ShowToast("Game Id copied to clipboard", 3);
            });
            newGameBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Copy Game Id";
            joinGameBtn.interactable = false;
            txtGameId.GetComponentInParent<TMP_InputField>().text = gamePda.ToString();
        }
        Loading.StopLoading();
    }

    private async UniTask JoinGame()
    {
        if(Web3.Account == null) return;
        Loading.StartLoading();
        var gameId = txtGameId.text.Trim().Replace("\u200B", "");
        var game = (await SolChessProgramClient.GetGameAsync(gameId, Commitment.Confirmed)).ParsedResult;
        _gameInstanceId = new PublicKey(gameId);
        SetGame(game);
        SubscribeToGame(_gameInstanceId).Forget();
        var userPda = FindUserPda(Web3.Account.PublicKey);
        switch (game?.GameState)
        {
            case GameState.Waiting:
            {
                var res = await JoinGameTransaction(gameId, game.White == null ? Color.White : Color.Black).AsUniTask();
                Debug.Log($"Signature: {res.Result}");
                await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed).AsUniTask();
                break;
            }
            case GameState.Black or GameState.White when game.Black.Equals(userPda) || game.White.Equals(userPda):
            {
                Debug.Log("Re-Joining a game");
                break;
            }
            default:
                throw new Exception("Invalid game state"); 
        }
        joinGameBtn.gameObject.transform.parent.parent.gameObject.SetActive(false);
    }
    
    private async void OnMove(Movement move)
    {
        if(Web3.Account == null) return;
        if(_gameInstanceId == null) return;
        var from = new SolChess.Types.Square()
        {
            File = (byte) UnMapFile(move.Start.File),
            Rank = (byte) UnMapRank(move.Start.Rank)
        };
        
        var to = new SolChess.Types.Square()
        {
            File = (byte) UnMapFile(move.End.File),
            Rank = (byte) UnMapRank(move.End.Rank)
        };
        var res = await MakeMoveTransaction(from, to);
        Debug.Log(res.Result);
    }

    private void SetGame(Game game)
    {
        Loading.StopLoading();
        if(game == null) throw new Exception("Game not found");
        List<(Square, Piece)> pieces = new List<(Square, Piece)>();
        
        for (var f = 0; f < game.Board.BoardField.Length; f++)
        {
            for (var i = 0; i < game.Board.BoardField[f].Length; i++)
            {
                var piece = game.Board.BoardField[f][i];
                if(piece == SolChess.Types.Piece.Empty) continue;
                var isWhite = piece.ToString().Contains("White");
                var pieceName = piece.ToString().Replace("White", "").Replace("Black", "");
                var pieceType = Type.GetType($"UnityChess.{pieceName}, UnityChessLib");
                if(pieceType == null) throw new Exception($"Invalid piece type: {pieceName}");
                var pieceInstance = Activator.CreateInstance(pieceType, isWhite ? Side.White : Side.Black);
                pieces.Add((new Square(MapFile(i), MapRank(f)), (Piece) pieceInstance));
            }
        }
        var conditions = new GameConditions(
            sideToMove: game.GameState == GameState.Black ? Side.Black : Side.White,
            whiteCanCastleKingside: game.CastlingRight.WhiteKingside,
            whiteCanCastleQueenside: game.CastlingRight.WhiteQueenside,
            blackCanCastleKingside: game.CastlingRight.BlackKingside,
            blackCanCastleQueenside: game.CastlingRight.BlackQueenside,
            enPassantSquare: game.Enpassant == null ? UnityChess.Square.Invalid : 
                new Square(MapFile(game.Enpassant.File), MapFile(game.Enpassant.Rank)),
            halfMoveClock: 0,
            turnNumber: 1
        );
        var unityGame = new UnityChess.Game(conditions, pieces.ToArray());
        GameManager.Instance.LoadGame(unityGame);
        var imAWhite = game.White != null && game.White.Equals(FindUserPda(Web3.Account.PublicKey));
        if(!imAWhite) GameManager.Instance.FlipBoard();
        if(game.GameState == GameState.White && !imAWhite || game.GameState == GameState.Black && imAWhite)
            BoardManager.Instance.SetActiveAllPieces(false);
    }


    #region Transactions

    private async Task<RequestResult<string>> JoinGameTransaction(string gameId, Color color, bool initializeGame = false)
    {
        var userPda = FindUserPda(Web3.Account);
        var accounts = new JoinGameAccounts()
        {
            Game = new PublicKey(gameId),
            Payer = Web3.Account,
            User = userPda
        };
        var joinGameIx = SolChessProgram.JoinGame(accounts: accounts, color, _solchessProgramId);

        var tx = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash()
        };
        if (!await IsPdaInitialized(userPda))
        {
            var initUserAccounts = new InitializeUserAccounts()
            {
                Payer = Web3.Account,
                User = userPda,
                SystemProgram = SystemProgram.ProgramIdKey,
            };
            var initUserIx = SolChessProgram.InitializeUser(initUserAccounts, _solchessProgramId);
            tx.Instructions.Add(initUserIx);
        }

        if (initializeGame)
        {
            var initializeGameAccounts = new InitializeGameAccounts()
            {
                Payer = Web3.Account,
                Game = new PublicKey(gameId),
                User = userPda,
                SystemProgram = SystemProgram.ProgramIdKey,
                Clock = SysVars.ClockKey
            };
            var initGameIx = SolChessProgram.InitializeGame(initializeGameAccounts, 0, false, _solchessProgramId);
            tx.Instructions.Add(initGameIx);
        }

        tx.Instructions.Add(joinGameIx);
        
        return await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
    }
    
    private async Task<RequestResult<string>> MakeMoveTransaction(SolChess.Types.Square from, SolChess.Types.Square to)
    {
        var accounts = new MovePieceAccounts()
        {
            Payer = Web3.Account,
            User = FindUserPda(Web3.Account),
            AdversaryUser = FindUserPda(Web3.Account),
            Game = _gameInstanceId
        };
        var movePieceIx = SolChessProgram.MovePiece(accounts, from, to, _solchessProgramId);
        var tx = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash()
        };
        tx.Instructions.Add(ComputeBudgetProgram.SetComputeUnitLimit(600000));
        tx.Instructions.Add(movePieceIx);
        return await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
    }

    #endregion

    #region pdas

    private PublicKey FindUserPda(PublicKey accountPublicKey)
    {
        PublicKey.TryFindProgramAddress(new[]
        {
            Encoding.UTF8.GetBytes("user"), accountPublicKey
        }, _solchessProgramId, out var pda, out _);
        return pda;
    }
    
    private PublicKey FindGamePda(PublicKey accountPublicKey, ulong gameId = 0)
    {
        PublicKey.TryFindProgramAddress(new[]
        {
            Encoding.UTF8.GetBytes("game"), accountPublicKey, BitConverter.GetBytes(gameId).Reverse().ToArray()
        }, _solchessProgramId, out var pda, out _);
        return pda;
    }
    
    private async UniTask<bool> IsPdaInitialized(PublicKey pda)
    {
        var accountInfoAsync = await Web3.Rpc.GetAccountInfoAsync(pda);
        return accountInfoAsync.WasSuccessful && accountInfoAsync.Result?.Value != null;
    }

    #endregion

    #region mappings

    private static int MapFile(int file)
    {
        return file + 1;
    }

    private static int MapRank(int rank)
    {
        return 8 - rank;
    }
    
    private static int UnMapFile(int file)
    {
        return file - 1;
    }

    private static int UnMapRank(int rank)
    {
        return 8 - rank;
    }

    #endregion

}