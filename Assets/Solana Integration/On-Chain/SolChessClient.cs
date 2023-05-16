using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Core.Http;
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
using Square = SolChess.Types.Square;

// ReSharper disable once CheckNamespace

public class SolChessClient : MonoBehaviour
{
    //private static readonly PublicKey ProgramId = new("AH3AYE6ZSZEGqeDiAGjCT9UdGakK3nZbzPmAKcbdKC3W");
    private static readonly PublicKey ProgramId = new("CCdU3zmYqPZaR2twy5hqcJmFV36tRpFC81seKUE8HVwX");

    [SerializeField] 
    private Button newGameBtn;
    [SerializeField] 
    private Button joinGameBtn;
    [SerializeField] 
    private TextMeshProUGUI txtGameId;

    private Game _game;
    private PublicKey _gameAddress;

    
    private SolChess.SolChessClient _solChessProgramInternal;
    private SolChess.SolChessClient SolChessProgram => _solChessProgramInternal ??= new SolChess.SolChessClient(Web3.Rpc, Web3.WsRpc, ProgramId);

    
    private void Start()
    {
        joinGameBtn.onClick.AddListener(async () =>
        {
            _gameAddress = new PublicKey(txtGameId.text.Trim().Replace("\u200B", ""));
            var game = (await SolChessProgram.GetGameAsync(_gameAddress))?.ParsedResult;
            SetGame(game);
            var user = FindUserPda(Web3.Account);
            switch (_game?.GameState)
            {
                case GameState.Waiting:
                {
                    var res = await JoinGame(_gameAddress, _game.White == null ? Color.White : Color.Black);
                    Debug.Log("JoinGameTx: " + res.Result);
                    break;
                }
                case GameState.Black or GameState.White 
                      when _game.White.Equals(user) || _game.Black.Equals(user):
                    Debug.Log("Re-Joining the game");
                    break;
                default:
                    throw new Exception("Game is not in a joinable state");
            }
            SubscribeToGame(_gameAddress).Forget();
        });
        
        newGameBtn.onClick.AddListener(async () =>
        {
            _gameAddress = FindGamePda(FindUserPda(Web3.Account));
            var res = await InitializeAndJoinGame(_gameAddress);
            Debug.Log("NewGameTx: " + res.Result);
            SubscribeToGame(_gameAddress).Forget();

        });

    }

    private async UniTask SubscribeToGame(PublicKey gameAddress)
    {
        await SolChessProgram.SubscribeGameAsync(gameAddress,
            (_, gameInfo, gameUpdate) =>
            {
                Debug.Log("Game changed");
                Debug.Log(gameUpdate);
                SetGame(gameUpdate);
            },
            Commitment.Confirmed
        );
    }

    private void OnEnable()
    {
        GameManager.MoveEvent += GameManagerOnMoveEvent;
    }

    private void OnDisable()
    {
        GameManager.MoveEvent -= GameManagerOnMoveEvent;
    }

    private void SetGame(Game game)
    {
        List<(UnityChess.Square, UnityChess.Piece)> pieces = new List<(UnityChess.Square, UnityChess.Piece)>();
        _game = game;
        for (var f = 0; f < game.Board.BoardField.Length; f++)
        {
            for (var i = 0; i < game.Board.BoardField[f].Length; i++)
            {
                var piece = game.Board.BoardField[f][i];
                if (piece == SolChess.Types.Piece.Empty) continue;
                var isWhite = piece.ToString().Contains("White");
                var pieceName = piece.ToString().Replace("Black", "").Replace("White", "");
                var pieceType = Type.GetType($"UnityChess.{pieceName}, UnityChessLib");
                if(pieceType == null) throw new Exception($"Piece type {pieceName} not found");
                var pieceInstance = Activator.CreateInstance(pieceType, isWhite ? Side.White : Side.Black);
                pieces.Add((new UnityChess.Square(MapFile(i), MapRank(f)), pieceInstance as UnityChess.Piece));
            }
        }
        var conditions = new GameConditions(
            sideToMove: game.GameState == GameState.Black ? Side.Black : Side.White,
            whiteCanCastleKingside: game.CastlingRight.WhiteKingside,
            whiteCanCastleQueenside: game.CastlingRight.WhiteQueenside,
            blackCanCastleKingside: game.CastlingRight.BlackKingside,
            blackCanCastleQueenside: game.CastlingRight.BlackQueenside,
            enPassantSquare: game.Enpassant == null ? UnityChess.Square.Invalid : 
                new UnityChess.Square(MapFile(game.Enpassant.File), MapRank(game.Enpassant.Rank)),
            halfMoveClock: 0,
            turnNumber: 1
        );
        GameManager.Instance.LoadGame(new UnityChess.Game(conditions, pieces.ToArray()));
        var amIWhite = game.White == null || game.White.Equals(FindUserPda(Web3.Account));
        if(!amIWhite) GameManager.Instance.FlipBoard();
        if (_game.GameState == GameState.White && !amIWhite || _game.GameState == GameState.Black && amIWhite)
        {
            BoardManager.Instance.SetActiveAllPieces(false);
        }
    }

    private async void GameManagerOnMoveEvent(Movement move)
    {
        var from = new Square()
        {
            File = (byte) UnMapFile(move.Start.File),
            Rank = (byte) UnMapRank(move.Start.Rank)
        };
        var to = new Square()
        {
            File = (byte) UnMapFile(move.End.File),
            Rank = (byte) UnMapRank(move.End.Rank)
        };
        var res = await MakeMove(from, to);
        Debug.Log("MoveTx: " + res.Result); 
    }
    
    private async Task<RequestResult<string>> InitializeAndJoinGame(PublicKey gameAddress, Color color = Color.White)
    {
        var accounts = new InitializeGameAccounts()
        {
            Game = gameAddress,
            Payer = Web3.Account,
            User = FindUserPda(Web3.Account),
            SystemProgram = SystemProgram.ProgramIdKey,
            Clock = SysVars.ClockKey
        };
        var joinGameAccounts = new JoinGameAccounts()
        {
            Game = gameAddress,
            Payer = Web3.Account,
            User = FindUserPda(Web3.Account)
        };
        var tx = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash()
        };
        if (!await IsUserInitialized())
        {
            var userInitAccounts = new InitializeUserAccounts()
            {
                Payer = Web3.Account,
                User = FindUserPda(Web3.Account),
                SystemProgram = SystemProgram.ProgramIdKey
            };
            tx.Instructions.Add(SolChess.Program.SolChessProgram.InitializeUser(userInitAccounts, ProgramId));
        }
        tx.Instructions.Add(SolChess.Program.SolChessProgram.InitializeGame(accounts,0, true, ProgramId));
        tx.Instructions.Add(SolChess.Program.SolChessProgram.JoinGame(joinGameAccounts, color, ProgramId));
        return await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
    }


    private async Task<RequestResult<string>> JoinGame(PublicKey gameAddress, Color color = Color.White)
    {
        var accounts = new JoinGameAccounts()
        {
            Game = gameAddress,
            Payer = Web3.Account,
            User = FindUserPda(Web3.Account)
        };
        var tx = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash()
        };
        if (!await IsUserInitialized())
        {
            var userInitAccounts = new InitializeUserAccounts()
            {
                Payer = Web3.Account,
                User = FindUserPda(Web3.Account),
                SystemProgram = SystemProgram.ProgramIdKey
            };
            tx.Instructions.Add(SolChess.Program.SolChessProgram.InitializeUser(userInitAccounts, ProgramId));
        }
        tx.Instructions.Add(SolChess.Program.SolChessProgram.JoinGame(accounts, color, ProgramId));
        return await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
    }
    
    private async Task<RequestResult<string>> MakeMove(Square from, Square to)
    {
        var accounts = new MovePieceAccounts()
        {
            Game = _gameAddress,
            Payer = Web3.Account,
            User = FindUserPda(Web3.Account),
            AdversaryUser = FindUserPda(Web3.Account)
        };
        var tx = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash()
        };
        tx.Instructions.Add(ComputeBudgetProgram.SetComputeUnitLimit(600000));
        tx.Instructions.Add(SolChess.Program.SolChessProgram.MovePiece(accounts, from, to, ProgramId));
        return await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
    }

    private async Task<bool> IsUserInitialized()
    {
        var userPda = FindUserPda(Web3.Account);
        var userAccountInfo = await Web3.Rpc.GetAccountInfoAsync(userPda);
        return userAccountInfo.WasSuccessful && userAccountInfo.Result?.Value != null;
    }

    #region utils

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

    #region PDAs

    private static PublicKey FindUserPda(PublicKey address)
    {
        PublicKey.TryFindProgramAddress(new[]
        {
            Encoding.UTF8.GetBytes("user"), address.KeyBytes 
        }, ProgramId,  out var userPda, out _);
        return userPda;
    }
    
    private static PublicKey FindGamePda(PublicKey address, ulong id = 0)
    {
        PublicKey.TryFindProgramAddress(new[]
        {
            Encoding.UTF8.GetBytes("game"), address.KeyBytes, BitConverter.GetBytes(id).Reverse().ToArray()
        }, ProgramId,  out var gamePda, out _);
        return gamePda;
    }

    #endregion
}