// Copyright (C) 2015-2026 The Neo Project.
//
// RpcServer.Blockchain.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;
using Neo.Extensions;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.RpcServer.Model;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Array = Neo.VM.Types.Array;

namespace Neo.Plugins.RpcServer;

partial class RpcServer
{
    /// <summary>
    /// Gets the hash of the best (most recent) block.
    /// <para>Request format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "method": "getbestblockhash"}</code>
    /// <para>Response format:</para>
    /// <code>
    /// {"jsonrpc": "2.0", "id": 1, "result": "0x...64 hex digits..."}
    /// </code>
    /// </summary>
    /// <returns>The hash of the best block as a <see cref="JToken"/>.</returns>
    [RpcMethod]
    protected internal virtual JToken GetBestBlockHash()
    {
        return NativeContract.Ledger.CurrentHash(system.StoreView).ToString();
    }

    /// <summary>
    /// Gets a block by its hash or index.
    /// <para>Request format:</para>
    /// <code>
    /// // Request with block hash(for example: 0x6c0b6c03fbc7d7d797ddd6483fe59a64f77c47475c1da600b71b189f6f4f234a)
    /// {"jsonrpc": "2.0", "id": 1, "method": "getblock", "params": ["0x...64 hex digits..."]}
    /// </code>
    /// <code>
    /// // Request with block index
    /// {"jsonrpc": "2.0", "id": 1, "method": "getblock", "params": [100]}
    /// </code>
    /// <code>
    /// // Request with block hash and verbose is true
    /// {"jsonrpc": "2.0", "id": 1, "method": "getblock", "params": ["0x...64 hex digits...", true]}
    /// </code>
    /// <para>Response format:</para>
    /// <code>
    /// {"jsonrpc": "2.0", "id": 1, "result": "AQID"}
    /// </code>
    /// <para>If verbose is true, the response format is:</para>
    /// <code>{
    ///   "jsonrpc":"2.0",
    ///   "id":1,
    ///   "result":{
    ///     "hash":"0x...64 hex digits...",
    ///     "size":697,
    ///     "version":0,
    ///     "previousblockhash":"0x...64 hex digits...",
    ///     "merkleroot":"0x...64 hex digits...",
    ///     "time":1627896461306,
    ///     "nonce":"09D4422954577BCE",
    ///     "index":100,
    ///     "primary":2,
    ///     "nextconsensus":"N...",
    ///     "witnesses":[{"invocation":"AQID","verification":"BAUG"}],
    ///     "tx":[],
    ///     "confirmations":200,
    ///     "nextblockhash":"0x...64 hex digits..."
    ///   }
    /// }</code>
    /// <para>
    /// <c>tx</c> is an array of verbose transaction objects. <c>nextblockhash</c>
    /// is omitted for the current tip.
    /// </para>
    /// </summary>
    /// <param name="blockHashOrIndex">The block hash or index.</param>
    /// <param name="verbose">Optional, the default value is false.</param>
    /// <returns>The block data as a <see cref="JToken"/>. If <paramref name="verbose"/>
    /// is true, this is a JSON object; otherwise it is a Base64 string.</returns>
    [RpcMethod]
    protected internal virtual JToken GetBlock(BlockHashOrIndex blockHashOrIndex, bool verbose = false)
    {
        blockHashOrIndex.NotNull_Or(RpcError.InvalidParams.WithData($"Invalid 'blockHashOrIndex'"));

        using var snapshot = system.GetSnapshotCache();
        var block = blockHashOrIndex.IsIndex
            ? NativeContract.Ledger.GetBlock(snapshot, blockHashOrIndex.AsIndex())
            : NativeContract.Ledger.GetBlock(snapshot, blockHashOrIndex.AsHash());
        block.NotNull_Or(RpcError.UnknownBlock);
        if (verbose)
        {
            JObject json = block.ToJson(system.Settings);
            json["confirmations"] = NativeContract.Ledger.CurrentIndex(snapshot) - block.Index + 1;
            UInt256? hash = NativeContract.Ledger.GetBlockHash(snapshot, block.Index + 1);
            if (hash != null)
                json["nextblockhash"] = hash.ToString();
            return json;
        }
        return Convert.ToBase64String(block.ToArray());
    }

    /// <summary>
    /// Gets the number of block headers in the blockchain.
    /// <para>Request format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "method": "getblockheadercount"}</code>
    /// <para>Response format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "result": 100 /* The number of block headers in the blockchain */}</code>
    /// </summary>
    /// <returns>The count of block headers as a <see cref="JToken"/>.</returns>
    [RpcMethod]
    internal virtual JToken GetBlockHeaderCount()
    {
        return (system.HeaderCache.Last?.Index ?? NativeContract.Ledger.CurrentIndex(system.StoreView)) + 1;
    }

    /// <summary>
    /// Gets the number of blocks in the blockchain.
    /// <para>Request format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "method": "getblockcount"}</code>
    /// <para>Response format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "result": 100 /* The number of blocks in the blockchain */}</code>
    /// </summary>
    /// <returns>The count of blocks as a <see cref="JToken"/>.</returns>
    [RpcMethod]
    protected internal virtual JToken GetBlockCount()
    {
        return NativeContract.Ledger.CurrentIndex(system.StoreView) + 1;
    }

    /// <summary>
    /// Gets the hash of the block at the specified height.
    /// <para>Request format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "method": "getblockhash", "params": [100] /* The block index */}</code>
    /// <para>Response format:</para>
    /// <code>
    /// {"jsonrpc": "2.0", "id": 1, "result": "0x...64 hex digits..."}
    /// </code>
    /// </summary>
    /// <param name="height">Block index (block height)</param>
    /// <returns>The hash of the block at the specified height as a <see cref="JToken"/>.</returns>
    [RpcMethod]
    protected internal virtual JToken GetBlockHash(uint height)
    {
        var snapshot = system.StoreView;
        return NativeContract.Ledger.GetBlockHash(snapshot, height)?.ToString()
            ?? throw new RpcException(RpcError.UnknownHeight);
    }

    /// <summary>
    /// Gets a block header by its hash or index.
    /// <remarks>
    /// When verbose is false, the serialized header is returned as a Base64 string.
    /// When verbose is true, detailed header information is returned as a JSON object.
    /// Boolean values can be encoded as JSON booleans or as 0/1.
    /// </remarks>
    /// <para>Request format:</para>
    /// <code>
    /// // Request with block hash(for example: 0x6c0b6c03fbc7d7d797ddd6483fe59a64f77c47475c1da600b71b189f6f4f234a)
    /// {"jsonrpc": "2.0", "id": 1, "method": "getblockheader", "params": ["0x...64 hex digits..."]}
    /// </code>
    /// <code>
    /// // Request with block index
    /// {"jsonrpc": "2.0", "id": 1, "method": "getblockheader", "params": [100]}
    /// </code>
    /// <code>
    /// // Request with block index and verbose is true
    /// {"jsonrpc": "2.0", "id": 1, "method": "getblockheader", "params": [100, true]}
    /// </code>
    /// <para>Response format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "result": "AQID"}</code>
    /// <para>If verbose is true, the response format is:</para>
    /// <code>{
    ///   "jsonrpc":"2.0",
    ///   "id":1,
    ///   "result": {
    ///     "hash": "0x...64 hex digits...",
    ///     "size": 696,
    ///     "version": 0,
    ///     "previousblockhash": "0x...64 hex digits...",
    ///     "merkleroot": "0x...64 hex digits...",
    ///     "time": 1627896461306,
    ///     "nonce": "09D4422954577BCE",
    ///     "index": 100,
    ///     "primary": 2,
    ///     "nextconsensus": "N...",
    ///     "witnesses": [{"invocation":"AQID", "verification":"BAUG"}],
    ///     "confirmations": 200,
    ///     "nextblockhash": "0x...64 hex digits..."
    ///   }
    /// }</code>
    /// </summary>
    /// <param name="blockHashOrIndex">The block hash or index; the index is also the block height.</param>
    /// <param name="verbose">Optional; the default value is false.</param>
    /// <returns>
    /// The block header data as a <see cref="JToken"/>.
    /// A JSON object if <paramref name="verbose"/> is true; otherwise a Base64 string.
    /// </returns>
    [RpcMethod]
    protected internal virtual JToken GetBlockHeader(BlockHashOrIndex blockHashOrIndex, bool verbose = false)
    {
        blockHashOrIndex.NotNull_Or(RpcError.InvalidParams.WithData($"Invalid 'blockHashOrIndex'"));

        var snapshot = system.StoreView;
        Header header;
        if (blockHashOrIndex.IsIndex)
        {
            header = NativeContract.Ledger.GetHeader(snapshot, blockHashOrIndex.AsIndex()).NotNull_Or(RpcError.UnknownBlock);
        }
        else
        {
            header = NativeContract.Ledger.GetHeader(snapshot, blockHashOrIndex.AsHash()).NotNull_Or(RpcError.UnknownBlock);
        }

        if (verbose)
        {
            var json = header.ToJson(system.Settings);
            json["confirmations"] = NativeContract.Ledger.CurrentIndex(snapshot) - header.Index + 1;

            var hash = NativeContract.Ledger.GetBlockHash(snapshot, header.Index + 1);
            if (hash != null) json["nextblockhash"] = hash.ToString();
            return json;
        }

        return Convert.ToBase64String(header.ToArray());
    }

    /// <summary>
    /// Gets the state of a contract by its ID or script hash or (only for native contracts) by case-insensitive name.
    /// <para>Request format:</para>
    /// <code>
    /// {"jsonrpc": "2.0", "id": 1, "method": "getcontractstate", "params": ["0x...40 hex digits..."]}
    /// </code>
    /// <para>
    /// The parameter may also be a numeric contract ID or the case-insensitive
    /// name of an active native contract.
    /// </para>
    /// <para>Response format:</para>
    /// <code>{
    ///   "jsonrpc": "2.0",
    ///   "id": 1,
    ///   "result": {
    ///     "id": -1,
    ///     "updatecounter": 0,
    ///     "hash": "0x...40 hex digits...",
    ///     "nef": {},
    ///     "manifest": {}
    ///   }
    /// }</code>
    /// </summary>
    /// <param name="contractNameOrHashOrId">A contract ID, script hash, or
    /// case-insensitive native contract name.</param>
    /// <returns>The contract state as a JSON object.</returns>
    [RpcMethod]
    protected internal virtual JToken GetContractState(ContractNameOrHashOrId contractNameOrHashOrId)
    {
        contractNameOrHashOrId.NotNull_Or(RpcError.InvalidParams.WithData($"Invalid 'contractNameOrHashOrId'"));

        if (contractNameOrHashOrId.IsId)
        {
            var contractState = NativeContract.ContractManagement.GetContractById(system.StoreView, contractNameOrHashOrId.AsId());
            return contractState.NotNull_Or(RpcError.UnknownContract).ToJson();
        }

        var hash = contractNameOrHashOrId.IsName ? ToScriptHash(contractNameOrHashOrId.AsName()) : contractNameOrHashOrId.AsHash();
        var contract = NativeContract.ContractManagement.GetContract(system.StoreView, hash);
        return contract.NotNull_Or(RpcError.UnknownContract).ToJson();
    }

    private static UInt160 ToScriptHash(string keyword)
    {
        foreach (var native in NativeContract.Contracts)
        {
            if (keyword.Equals(native.Name, StringComparison.InvariantCultureIgnoreCase) || keyword == native.Id.ToString())
                return native.Hash;
        }

        return UInt160.Parse(keyword);
    }

    /// <summary>
    /// Gets the current memory pool transactions.
    /// <para>Request format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "method": "getrawmempool", "params": [true/*shouldGetUnverified, optional*/]}</code>
    /// <para>Response format:</para>
    /// If shouldGetUnverified is true, the response format is:
    /// <code>{
    ///   "jsonrpc": "2.0",
    ///   "id": 1,
    ///   "result": {
    ///     "height": 100,
    ///     "verified": ["The tx hash"], // The verified transactions
    ///     "unverified": ["The tx hash"] // The unverified transactions
    ///   }
    /// }</code>
    /// If shouldGetUnverified is false, the response format is:
    /// <code>{
    ///   "jsonrpc": "2.0",
    ///   "id": 1,
    ///   "result": ["The tx hash"] // The verified transactions
    /// }</code>
    /// </summary>
    /// <param name="shouldGetUnverified">Optional, the default value is false.</param>
    /// <returns>The memory pool transactions in json format as a <see cref="JToken"/>.</returns>
    [RpcMethod]
    protected internal virtual JToken GetRawMemPool(bool shouldGetUnverified = false)
    {
        if (!shouldGetUnverified)
            return new JArray(system.MemPool.GetVerifiedTransactions().Select(p => (JToken)p.Hash.ToString()));

        JObject json = new();
        json["height"] = NativeContract.Ledger.CurrentIndex(system.StoreView);
        system.MemPool.GetVerifiedAndUnverifiedTransactions(
            out IEnumerable<Transaction> verifiedTransactions,
            out IEnumerable<Transaction> unverifiedTransactions);
        json["verified"] = new JArray(verifiedTransactions.Select(p => (JToken)p.Hash.ToString()));
        json["unverified"] = new JArray(unverifiedTransactions.Select(p => (JToken)p.Hash.ToString()));
        return json;
    }

    /// <summary>
    /// Gets a transaction by its hash.
    /// <para>Request format:</para>
    /// <code>
    /// {"jsonrpc": "2.0", "id": 1, "method": "getrawtransaction", "params": ["0x...64 hex digits...", true]}
    /// </code>
    /// <para>Response format:</para>
    /// If verbose is false, the response format is:
    /// <code>{
    ///   "jsonrpc": "2.0",
    ///   "id": 1,
    ///   "result": "AQID"
    /// }</code>
    /// If verbose is true, the response format is:
    /// <code>{
    ///   "jsonrpc": "2.0",
    ///   "id": 1,
    ///   "result": {
    ///     "hash": "0x...64 hex digits...",
    ///     "size": 272,
    ///     "version": 0,
    ///     "nonce": 1553700339,
    ///     "sender": "N...",
    ///     "sysfee": "100000000",
    ///     "netfee": "1272390",
    ///     "validuntilblock": 2105487,
    ///     "attributes": [],
    ///     "signers": [],
    ///     "script": "AQID",
    ///     "witnesses": [{"invocation": "AQID", "verification": "BAUG"}],
    ///     "blockhash": "0x...64 hex digits...",
    ///     "confirmations": 100,
    ///     "blocktime": 1627896461306
    ///   }
    /// }</code>
    /// <para>
    /// <c>blockhash</c>, <c>confirmations</c>, and <c>blocktime</c> are present
    /// only when the transaction is confirmed.
    /// </para>
    /// </summary>
    /// <param name="hash">The transaction hash.</param>
    /// <param name="verbose">Optional, the default value is false.</param>
    /// <returns>The transaction data as a <see cref="JToken"/>. A JSON object
    /// if <paramref name="verbose"/> is true; otherwise a Base64 string.</returns>
    [RpcMethod]
    protected internal virtual JToken GetRawTransaction(UInt256 hash, bool verbose = false)
    {
        hash.NotNull_Or(RpcError.InvalidParams.WithData($"Invalid 'hash'"));

        if (system.MemPool.TryGetValue(hash, out var tx) && !verbose)
            return Convert.ToBase64String(tx.ToArray());
        var snapshot = system.StoreView;
        var state = NativeContract.Ledger.GetTransactionState(snapshot, hash);

        tx ??= state?.Transaction;
        tx.NotNull_Or(RpcError.UnknownTransaction);

        if (!verbose) return Convert.ToBase64String(tx.ToArray());

        var json = tx!.ToJson(system.Settings);
        if (state is not null)
        {
            var block = NativeContract.Ledger.GetTrimmedBlock(snapshot, NativeContract.Ledger.GetBlockHash(snapshot, state.BlockIndex)!)!;
            json["blockhash"] = block.Hash.ToString();
            json["confirmations"] = NativeContract.Ledger.CurrentIndex(snapshot) - block.Index + 1;
            json["blocktime"] = block.Header.Timestamp;
        }
        return json;
    }

    private static int GetContractId(IReadOnlyStore snapshot, ContractNameOrHashOrId contractNameOrHashOrId)
    {
        if (contractNameOrHashOrId.IsId) return contractNameOrHashOrId.AsId();

        var hash = contractNameOrHashOrId.IsName
            ? ToScriptHash(contractNameOrHashOrId.AsName())
            : contractNameOrHashOrId.AsHash();
        var contract = NativeContract.ContractManagement.GetContract(snapshot, hash).NotNull_Or(RpcError.UnknownContract);
        return contract.Id;
    }

    /// <summary>
    /// Gets the storage item by contract ID or script hash and key.
    /// <para>Request format:</para>
    /// <code>
    /// {
    ///   "jsonrpc": "2.0",
    ///   "id": 1,
    ///   "method": "getstorage",
    ///   "params": ["The contract id(int),  hash(UInt160) or native contract name(string)", "The Base64-encoded key"]
    /// }
    /// </code>
    /// <para>Response format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "result": "The Base64-encoded storage value"}</code>
    /// </summary>
    /// <param name="contractNameOrHashOrId">The contract ID, script hash, or
    /// case-insensitive native contract name.</param>
    /// <param name="base64Key">The Base64-encoded storage key.</param>
    /// <returns>The storage item as a <see cref="JToken"/>.</returns>
    [RpcMethod]
    protected internal virtual JToken GetStorage(ContractNameOrHashOrId contractNameOrHashOrId, string base64Key)
    {
        contractNameOrHashOrId.NotNull_Or(RpcError.InvalidParams.WithData($"Invalid 'contractNameOrHashOrId'"));
        base64Key.NotNull_Or(RpcError.InvalidParams.WithData($"Invalid 'base64Key'"));

        using var snapshot = system.GetSnapshotCache();
        int id = GetContractId(snapshot, contractNameOrHashOrId);

        var key = Convert.FromBase64String(base64Key);
        var item = snapshot.TryGet(new StorageKey
        {
            Id = id,
            Key = key
        }).NotNull_Or(RpcError.UnknownStorageItem);
        return Convert.ToBase64String(item.Value.Span);
    }

    /// <summary>
    /// Finds storage items by contract ID or script hash and prefix.
    /// Pagination uses an exclusive key cursor (like <c>findstates</c>), not a skip count,
    /// so continuing after storage mutations does not skip or duplicate keys around the cursor.
    /// <para>Request format:</para>
    /// <code>{
    ///   "jsonrpc": "2.0",
    ///   "id": 1,
    ///   "method": "findstorage",
    ///   "params": [
    ///     "The contract id(int), hash(UInt160) or native contract name(string)",
    ///     "The base64-encoded key prefix",
    ///     "" /* Optional exclusive start key (base64), usually previous result.next */
    ///   ]
    /// }</code>
    /// <para>Response format:</para>
    /// <code>{
    ///   "jsonrpc": "2.0",
    ///   "id": 1,
    ///   "result": {
    ///     "truncated": true,
    ///     "next": "AQID",
    ///     "results": [
    ///       {"key": "AQID", "value": "BAUG"}
    ///     ]
    ///   }
    /// }</code>
    /// <para>
    /// <c>next</c> is the last key returned on the page. If the page contains no
    /// new items, it is the supplied start value (or the empty string when no
    /// start was supplied).
    /// </para>
    /// </summary>
    /// <param name="contractNameOrHashOrId">The contract ID, script hash, or
    /// case-insensitive native contract name.</param>
    /// <param name="base64KeyPrefix">The Base64-encoded storage key prefix.</param>
    /// <param name="start">
    /// Exclusive start key (same encoding as result keys). Omit or pass empty to start at the prefix.
    /// When continuing, pass the previous page's <c>next</c> (last returned key).
    /// </param>
    /// <returns>The found storage items <see cref="StorageItem"/> as a <see cref="JToken"/>.</returns>
    [RpcMethod]
    protected internal virtual JToken FindStorage(ContractNameOrHashOrId contractNameOrHashOrId, string base64KeyPrefix, byte[]? start = null)
    {
        contractNameOrHashOrId.NotNull_Or(RpcError.InvalidParams.WithData($"Invalid 'contractNameOrHashOrId'"));
        base64KeyPrefix.NotNull_Or(RpcError.InvalidParams.WithData($"Invalid 'base64KeyPrefix'"));

        using var snapshot = system.GetSnapshotCache();
        int id = GetContractId(snapshot, contractNameOrHashOrId);

        var prefix = Result.Ok_Or(
            () => Convert.FromBase64String(base64KeyPrefix),
            RpcError.InvalidParams.WithData($"Invalid Base64 string: {base64KeyPrefix}"));
        start ??= [];
        if (start.Length > 0 && !start.AsSpan().StartsWith(prefix))
            throw new RpcException(RpcError.InvalidParams.WithData("Invalid 'start': must start with the given prefix"));

        var prefixKey = StorageKey.CreateSearchPrefix(id, prefix);
        var seekKey = start.Length > 0
            ? StorageKey.CreateSearchPrefix(id, start)
            : prefixKey;

        var json = new JObject();
        var items = new JArray();
        int pageSize = settings.FindStoragePageSize;
        byte[]? lastKey = start.Length > 0 ? start : null;
        var hasMore = false;
        var count = 0;

        foreach (var (key, value) in snapshot.Seek(seekKey))
        {
            if (key.Id != id || !key.Key.Span.StartsWith(prefix))
                break;

            // Exclusive cursor: skip the start key itself when it still exists.
            if (start.Length > 0 && key.Key.Span.SequenceEqual(start))
                continue;

            if (count == pageSize)
            {
                hasMore = true;
                break;
            }

            lastKey = key.Key.ToArray();
            items.Add(new JObject
            {
                ["key"] = Convert.ToBase64String(lastKey),
                ["value"] = Convert.ToBase64String(value.Value.Span)
            });
            count++;
        }

        json["truncated"] = hasMore;
        json["next"] = lastKey is null ? string.Empty : Convert.ToBase64String(lastKey);
        json["results"] = items;
        return json;
    }

    /// <summary>
    /// Gets the height of a transaction by its hash.
    /// <para>Request format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "method": "gettransactionheight", "params": ["The tx hash(UInt256)"]}</code>
    /// <para>Response format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "result": 100}</code>
    /// </summary>
    /// <param name="hash">The transaction hash.</param>
    /// <returns>The height of the transaction as a <see cref="JToken"/>.</returns>
    [RpcMethod]
    protected internal virtual JToken GetTransactionHeight(UInt256 hash)
    {
        hash.NotNull_Or(RpcError.InvalidParams.WithData($"Invalid 'hash'"));

        uint? height = NativeContract.Ledger.GetTransactionState(system.StoreView, hash)?.BlockIndex;
        if (height.HasValue) return height.Value;
        throw new RpcException(RpcError.UnknownTransaction);
    }

    /// <summary>
    /// Gets the next block validators.
    /// <para>Request format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "method": "getnextblockvalidators"}</code>
    /// <para>Response format:</para>
    /// <code>{
    ///   "jsonrpc": "2.0",
    ///   "id": 1,
    ///   "result": [
    ///     {"publickey": "The public key", "votes": 100 /* The votes of the validator */}
    ///     // ...
    ///   ]
    /// }</code>
    /// </summary>
    /// <returns>The next block validators as a <see cref="JToken"/>.</returns>
    [RpcMethod]
    protected internal virtual JToken GetNextBlockValidators()
    {
        using var snapshot = system.GetSnapshotCache();
        var validators = NativeContract.NEO.GetNextBlockValidators(snapshot, system.Settings.ValidatorsCount);
        return validators.Select(p =>
        {
            JObject validator = new();
            validator["publickey"] = p.ToString();
            validator["votes"] = (int)NativeContract.NEO.GetCandidateVote(snapshot, p);
            return validator;
        }).ToArray();
    }

    /// <summary>
    /// Gets the list of candidates for the next block validators.
    /// <para>Request format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "method": "getcandidates"}</code>
    /// <para>Response format:</para>
    /// <code>{
    ///   "jsonrpc": "2.0",
    ///   "id": 1,
    ///   "result": [
    ///     {"publickey": "The public key", "votes": "An integer number in string", "active": true /* Is active or not */}
    ///     // ...
    ///   ]
    /// }</code>
    /// </summary>
    /// <returns>The candidates public key list as a JToken.</returns>
    [RpcMethod]
    protected internal virtual JToken GetCandidates()
    {
        using var snapshot = system.GetSnapshotCache();
        byte[] script;
        using (ScriptBuilder sb = new())
        {
            script = sb.EmitDynamicCall(NativeContract.NEO.Hash, "getCandidates").ToArray();
        }
        StackItem[] resultstack;
        try
        {
            using var engine = ApplicationEngine.Run(script, snapshot, settings: system.Settings, gas: settings.MaxGasInvoke);
            resultstack = engine.ResultStack.ToArray();
        }
        catch
        {
            throw new RpcException(RpcError.InternalServerError.WithData("Can't get candidates."));
        }

        try
        {
            if (resultstack.Length == 0)
                return FormatCandidates(resultstack, []);

            var validators = NativeContract.NEO.GetNextBlockValidators(snapshot, system.Settings.ValidatorsCount)
                ?? throw new Exception();
            return FormatCandidates(resultstack, validators);
        }
        catch
        {
            throw new RpcException(RpcError.InternalServerError.WithData("Can't get next block validators"));
        }
    }

    /// <summary>
    /// Builds the getcandidates JSON array. An empty VM stack yields <c>[]</c>, not <c>{}</c>.
    /// </summary>
    internal static JToken FormatCandidates(StackItem[] resultstack, IReadOnlyCollection<ECPoint> validators)
    {
        var jArray = new JArray();
        if (resultstack.Length == 0)
            return jArray;

        foreach (var item in resultstack)
        {
            var value = (Array)item;
            foreach (Struct ele in value)
            {
                var publickey = ECPoint.DecodePoint(ele[0].GetSpan(), ECCurve.Secp256r1);
                jArray.Add(new JObject
                {
                    ["publickey"] = publickey.ToString(),
                    ["votes"] = ele[1].GetInteger().ToString(),
                    ["active"] = validators.Contains(publickey),
                });
            }
            return jArray;
        }

        return jArray;
    }

    /// <summary>
    /// Gets the list of committee members.
    /// <para>Request format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "method": "getcommittee"}</code>
    /// <para>Response format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "result": ["The public key"]}</code>
    /// </summary>
    /// <returns>The committee members publickeys as a <see cref="JToken"/>.</returns>
    [RpcMethod]
    protected internal virtual JToken GetCommittee()
    {
        return new JArray(NativeContract.NEO.GetCommittee(system.StoreView).Select(p => (JToken)p.ToString()));
    }

    /// <summary>
    /// Gets the list of native contracts.
    /// <para>Request format:</para>
    /// <code>{"jsonrpc": "2.0", "id": 1, "method": "getnativecontracts"}</code>
    /// <para>Response format:</para>
    /// <code>{
    ///   "jsonrpc": "2.0",
    ///   "id": 1,
    ///   "result": [{
    ///      "id": -1, // The contract id
    ///      "updatecounter": 0, // The update counter
    ///      "hash": "The contract hash(UInt160)", // The contract hash
    ///      "nef":  {
    ///        "magic": 0x3346454E, // The magic number, always 0x3346454E at present.
    ///        "compiler": "The compiler name",
    ///        "source": "The url of the source file",
    ///        "tokens": [
    ///          {
    ///            "hash": "The token hash(UInt160)",
    ///            "method": "The token method name",
    ///            "paramcount": 0, // The number of parameters
    ///            "hasreturnvalue": false, // Whether the method has a return value
    ///            "callflags": 0 // see CallFlags
    ///          } // A token in the contract
    ///          // ...
    ///        ],
    ///        "script": "The Base64-encoded script", // The Base64-encoded script
    ///        "checksum": 0x12345678 // The checksum
    ///      },
    ///      "manifest": {
    ///        "name": "The contract name",
    ///        "groups": [
    ///          {"pubkey": "The public key", "signature": "The signature"} // A group in the manifest
    ///        ],
    ///        "features": {}, // The features that the contract supports
    ///        "supportedstandards": ["The standard name"], // The standards that the contract supports
    ///        "abi": {
    ///          "methods": [
    ///            {
    ///              "name": "The method name",
    ///              "parameters": [
    ///                {"name": "The parameter name", "type": "The parameter type"} // A parameter in the method
    ///                // ...
    ///              ],
    ///              "returntype": "The return type",
    ///              "offset": 0, // The offset in script of the method
    ///              "safe": false // Whether the method is safe
    ///            } // A method in the abi
    ///            // ...
    ///          ],
    ///          "events": [
    ///            {
    ///              "name": "The event name",
    ///              "parameters": [
    ///                {"name": "The parameter name", "type": "The parameter type"} // A parameter in the event
    ///                // ...
    ///              ]
    ///            } // An event in the abi
    ///            // ...
    ///          ]
    ///        }, // The abi of the contract
    ///        "permissions": [
    ///          {
    ///            "contract": "The contract hash(UInt160), group(ECPoint), or '*'", // '*' means all contracts
    ///            "methods": ["The method name or '*'"] // '*' means all methods
    ///          } // A permission in the contract
    ///          // ...
    ///        ], // The permissions of the contract
    ///        "trusts": [
    ///          {
    ///            "contract": "The contract hash(UInt160), group(ECPoint), or '*'", // '*' means all contracts
    ///            "methods": ["The method name or '*'"] // '*' means all methods
    ///          } // A trust in the contract
    ///          // ...
    ///        ], // The trusts of the contract
    ///        "extra": {} // A json object, the extra content of the contract
    ///      } // The manifest of the contract
    ///    }]
    /// }</code>
    /// </summary>
    /// <returns>The native contract states <see cref="ContractState"/> as a <see cref="JToken"/>.</returns>
    [RpcMethod]
    protected internal virtual JToken GetNativeContracts()
    {
        var storeView = system.StoreView;
        var contractStates = NativeContract.Contracts
            .Select(p => NativeContract.ContractManagement.GetContract(storeView, p.Hash))
            .Where(p => p != null) // if not active
            .Select(p => p!.ToJson());
        return new JArray(contractStates);
    }
}
