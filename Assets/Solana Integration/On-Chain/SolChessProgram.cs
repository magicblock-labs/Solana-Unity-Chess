using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Solana.Unity;
using Solana.Unity.Programs.Abstract;
using Solana.Unity.Programs.Utilities;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using SolChess;
using SolChess.Program;
using SolChess.Errors;
using SolChess.Accounts;
using SolChess.Types;

namespace SolChess
{
    namespace Accounts
    {
        public partial class Game
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 1331205435963103771UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{27, 90, 166, 125, 74, 100, 121, 18};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "5aNQXizG8jB";
            public Board Board { get; set; }

            public GameState GameState { get; set; }

            public PublicKey White { get; set; }

            public PublicKey Black { get; set; }

            public Square Enpassant { get; set; }

            public CastlingRight CastlingRight { get; set; }

            public ulong? Wager { get; set; }

            public DrawState DrawState { get; set; }

            public long CreatedAt { get; set; }

            public bool IsRated { get; set; }

            public static Game Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                Game result = new Game();
                offset += Board.Deserialize(_data, offset, out var resultBoard);
                result.Board = resultBoard;
                result.GameState = (GameState)_data.GetU8(offset);
                offset += 1;
                if (_data.GetBool(offset++))
                {
                    result.White = _data.GetPubKey(offset);
                    offset += 32;
                }

                if (_data.GetBool(offset++))
                {
                    result.Black = _data.GetPubKey(offset);
                    offset += 32;
                }

                if (_data.GetBool(offset++))
                {
                    offset += Square.Deserialize(_data, offset, out var resultEnpassant);
                    result.Enpassant = resultEnpassant;
                }

                offset += CastlingRight.Deserialize(_data, offset, out var resultCastlingRight);
                result.CastlingRight = resultCastlingRight;
                if (_data.GetBool(offset++))
                {
                    result.Wager = _data.GetU64(offset);
                    offset += 8;
                }

                result.DrawState = (DrawState)_data.GetU8(offset);
                offset += 1;
                result.CreatedAt = _data.GetS64(offset);
                offset += 8;
                result.IsRated = _data.GetBool(offset);
                offset += 1;
                return result;
            }
        }

        public partial class User
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 17022084798167872927UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{159, 117, 95, 227, 239, 151, 58, 236};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "TfwwBiNJtao";
            public PublicKey CurrentGame { get; set; }

            public uint Elo { get; set; }

            public ulong Games { get; set; }

            public ulong Balance { get; set; }

            public static User Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                User result = new User();
                if (_data.GetBool(offset++))
                {
                    result.CurrentGame = _data.GetPubKey(offset);
                    offset += 32;
                }

                result.Elo = _data.GetU32(offset);
                offset += 4;
                result.Games = _data.GetU64(offset);
                offset += 8;
                result.Balance = _data.GetU64(offset);
                offset += 8;
                return result;
            }
        }
    }

    namespace Errors
    {
        public enum SolChessErrorKind : uint
        {
            UserAlreadyInGame = 6000U,
            ColorNotAvailable = 6001U,
            InvalidGameState = 6002U,
            NotUsersTurn = 6003U,
            InvalidMove = 6004U,
            KingInCheck = 6005U,
            InsufficientBalance = 6006U,
            NotInGame = 6007U,
            GameAlreadyStarted = 6008U,
            InvalidAdversaryUserAccount = 6009U,
            AlreadyInGame = 6010U,
            AlreadyOfferedDraw = 6011U,
            InvalidUser = 6012U
        }
    }

    namespace Types
    {
        public partial class Board
        {
            public Piece[][] BoardField { get; set; }

            public int Serialize(byte[] _data, int initialOffset)
            {
                int offset = initialOffset;
                foreach (var boardFieldElement in BoardField)
                {
                    foreach (var boardFieldElementElement in boardFieldElement)
                    {
                        _data.WriteU8((byte)boardFieldElementElement, offset);
                        offset += 1;
                    }
                }

                return offset - initialOffset;
            }

            public static int Deserialize(ReadOnlySpan<byte> _data, int initialOffset, out Board result)
            {
                int offset = initialOffset;
                result = new Board();
                result.BoardField = new Piece[8][];
                for (uint resultBoardFieldIdx = 0; resultBoardFieldIdx < 8; resultBoardFieldIdx++)
                {
                    result.BoardField[resultBoardFieldIdx] = new Piece[8];
                    for (uint resultBoardFieldresultBoardFieldIdxIdx = 0; resultBoardFieldresultBoardFieldIdxIdx < 8; resultBoardFieldresultBoardFieldIdxIdx++)
                    {
                        result.BoardField[resultBoardFieldIdx][resultBoardFieldresultBoardFieldIdxIdx] = (Piece)_data.GetU8(offset);
                        offset += 1;
                    }
                }

                return offset - initialOffset;
            }
        }

        public partial class CastlingRight
        {
            public bool WhiteKingside { get; set; }

            public bool WhiteQueenside { get; set; }

            public bool BlackKingside { get; set; }

            public bool BlackQueenside { get; set; }

            public int Serialize(byte[] _data, int initialOffset)
            {
                int offset = initialOffset;
                _data.WriteBool(WhiteKingside, offset);
                offset += 1;
                _data.WriteBool(WhiteQueenside, offset);
                offset += 1;
                _data.WriteBool(BlackKingside, offset);
                offset += 1;
                _data.WriteBool(BlackQueenside, offset);
                offset += 1;
                return offset - initialOffset;
            }

            public static int Deserialize(ReadOnlySpan<byte> _data, int initialOffset, out CastlingRight result)
            {
                int offset = initialOffset;
                result = new CastlingRight();
                result.WhiteKingside = _data.GetBool(offset);
                offset += 1;
                result.WhiteQueenside = _data.GetBool(offset);
                offset += 1;
                result.BlackKingside = _data.GetBool(offset);
                offset += 1;
                result.BlackQueenside = _data.GetBool(offset);
                offset += 1;
                return offset - initialOffset;
            }
        }

        public partial class Square
        {
            public byte Rank { get; set; }

            public byte File { get; set; }

            public int Serialize(byte[] _data, int initialOffset)
            {
                int offset = initialOffset;
                _data.WriteU8(Rank, offset);
                offset += 1;
                _data.WriteU8(File, offset);
                offset += 1;
                return offset - initialOffset;
            }

            public static int Deserialize(ReadOnlySpan<byte> _data, int initialOffset, out Square result)
            {
                int offset = initialOffset;
                result = new Square();
                result.Rank = _data.GetU8(offset);
                offset += 1;
                result.File = _data.GetU8(offset);
                offset += 1;
                return offset - initialOffset;
            }
        }

        public enum Color : byte
        {
            White,
            Black
        }

        public enum DrawState : byte
        {
            Neither,
            White,
            Black,
            Draw
        }

        public enum GameState : byte
        {
            Waiting,
            White,
            Black,
            WhiteWon,
            BlackWon,
            Draw
        }

        public enum Piece : byte
        {
            Empty,
            BlackPawn,
            BlackRook,
            BlackKnight,
            BlackBishop,
            BlackQueen,
            BlackKing,
            WhitePawn,
            WhiteRook,
            WhiteKnight,
            WhiteBishop,
            WhiteQueen,
            WhiteKing
        }
    }

    public partial class SolChessClient : TransactionalBaseClient<SolChessErrorKind>
    {
        public SolChessClient(IRpcClient rpcClient, IStreamingRpcClient streamingRpcClient, PublicKey programId) : base(rpcClient, streamingRpcClient, programId)
        {
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Game>>> GetGamesAsync(string programAddress, Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = Game.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Game>>(res);
            List<Game> resultingAccounts = new List<Game>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => Game.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<Game>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<User>>> GetUsersAsync(string programAddress, Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = User.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<User>>(res);
            List<User> resultingAccounts = new List<User>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => User.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<User>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<Game>> GetGameAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<Game>(res);
            var resultingAccount = Game.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<Game>(res, resultingAccount);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<User>> GetUserAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<User>(res);
            var resultingAccount = User.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<User>(res, resultingAccount);
        }

        public async Task<SubscriptionState> SubscribeGameAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, Game> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                Game parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = Game.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<SubscriptionState> SubscribeUserAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, User> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                User parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = User.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<RequestResult<string>> SendInitializeUserAsync(InitializeUserAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolChessProgram.InitializeUser(accounts, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendInitializeGameAsync(InitializeGameAccounts accounts, ulong? wager, bool isRated, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolChessProgram.InitializeGame(accounts, wager, isRated, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendJoinGameAsync(JoinGameAccounts accounts, Color color, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolChessProgram.JoinGame(accounts, color, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendMovePieceAsync(MovePieceAccounts accounts, Square from, Square to, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolChessProgram.MovePiece(accounts, from, to, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendDepositAsync(DepositAccounts accounts, ulong amount, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolChessProgram.Deposit(accounts, amount, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendWithdrawAsync(WithdrawAccounts accounts, ulong amount, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolChessProgram.Withdraw(accounts, amount, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendLeaveGameAsync(LeaveGameAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolChessProgram.LeaveGame(accounts, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendResignAsync(ResignAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolChessProgram.Resign(accounts, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendOfferDrawAsync(OfferDrawAccounts accounts, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.SolChessProgram.OfferDraw(accounts, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        protected override Dictionary<uint, ProgramError<SolChessErrorKind>> BuildErrorsDictionary()
        {
            return new Dictionary<uint, ProgramError<SolChessErrorKind>>{{6000U, new ProgramError<SolChessErrorKind>(SolChessErrorKind.UserAlreadyInGame, "User Already In Game")}, {6001U, new ProgramError<SolChessErrorKind>(SolChessErrorKind.ColorNotAvailable, "Color Not Available")}, {6002U, new ProgramError<SolChessErrorKind>(SolChessErrorKind.InvalidGameState, "Invalid Game State")}, {6003U, new ProgramError<SolChessErrorKind>(SolChessErrorKind.NotUsersTurn, "Not User's Turn")}, {6004U, new ProgramError<SolChessErrorKind>(SolChessErrorKind.InvalidMove, "Invalid Move")}, {6005U, new ProgramError<SolChessErrorKind>(SolChessErrorKind.KingInCheck, "King in Check")}, {6006U, new ProgramError<SolChessErrorKind>(SolChessErrorKind.InsufficientBalance, "Insufficient Balance")}, {6007U, new ProgramError<SolChessErrorKind>(SolChessErrorKind.NotInGame, "Not In Game")}, {6008U, new ProgramError<SolChessErrorKind>(SolChessErrorKind.GameAlreadyStarted, "Game Already Started")}, {6009U, new ProgramError<SolChessErrorKind>(SolChessErrorKind.InvalidAdversaryUserAccount, "Invalid Adversary User Account")}, {6010U, new ProgramError<SolChessErrorKind>(SolChessErrorKind.AlreadyInGame, "User Already In Game")}, {6011U, new ProgramError<SolChessErrorKind>(SolChessErrorKind.AlreadyOfferedDraw, "Already Offered Draw")}, {6012U, new ProgramError<SolChessErrorKind>(SolChessErrorKind.InvalidUser, "Invalid User")}, };
        }
    }

    namespace Program
    {
        public class InitializeUserAccounts
        {
            public PublicKey Payer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class InitializeGameAccounts
        {
            public PublicKey Payer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey Game { get; set; }

            public PublicKey SystemProgram { get; set; }

            public PublicKey Clock { get; set; }
        }

        public class JoinGameAccounts
        {
            public PublicKey Payer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey Game { get; set; }
        }

        public class MovePieceAccounts
        {
            public PublicKey Payer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey AdversaryUser { get; set; }

            public PublicKey Game { get; set; }

            public PublicKey SessionToken { get; set; }
        }

        public class DepositAccounts
        {
            public PublicKey Payer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey Vault { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class WithdrawAccounts
        {
            public PublicKey Payer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey Vault { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class LeaveGameAccounts
        {
            public PublicKey Payer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey Game { get; set; }
        }

        public class ResignAccounts
        {
            public PublicKey Payer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey AdversaryUser { get; set; }

            public PublicKey Game { get; set; }
        }

        public class OfferDrawAccounts
        {
            public PublicKey Payer { get; set; }

            public PublicKey User { get; set; }

            public PublicKey AdversaryUser { get; set; }

            public PublicKey Game { get; set; }
        }

        public static class SolChessProgram
        {
            public static Solana.Unity.Rpc.Models.TransactionInstruction InitializeUser(InitializeUserAccounts accounts, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(18313459337071759727UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction InitializeGame(InitializeGameAccounts accounts, ulong? wager, bool isRated, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Game, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.Clock, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(15529203708862021164UL, offset);
                offset += 8;
                if (wager != null)
                {
                    _data.WriteU8(1, offset);
                    offset += 1;
                    _data.WriteU64(wager.Value, offset);
                    offset += 8;
                }
                else
                {
                    _data.WriteU8(0, offset);
                    offset += 1;
                }

                _data.WriteBool(isRated, offset);
                offset += 1;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction JoinGame(JoinGameAccounts accounts, Color color, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Game, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(9240450992125931627UL, offset);
                offset += 8;
                _data.WriteU8((byte)color, offset);
                offset += 1;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction MovePiece(MovePieceAccounts accounts, Square from, Square to, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.AdversaryUser, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Game, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(5542210051077342600UL, offset);
                offset += 8;
                offset += from.Serialize(_data, offset);
                offset += to.Serialize(_data, offset);
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction Deposit(DepositAccounts accounts, ulong amount, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Vault, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(13182846803881894898UL, offset);
                offset += 8;
                _data.WriteU64(amount, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction Withdraw(WithdrawAccounts accounts, ulong amount, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Vault, false), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(2495396153584390839UL, offset);
                offset += 8;
                _data.WriteU64(amount, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction LeaveGame(LeaveGameAccounts accounts, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Game, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(14518799200785195738UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction Resign(ResignAccounts accounts, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.AdversaryUser, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Game, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(16271106710546526641UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction OfferDraw(OfferDrawAccounts accounts, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Payer, true), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.User, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.AdversaryUser, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Game, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(16562138593112952919UL, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }
        }
    }
}