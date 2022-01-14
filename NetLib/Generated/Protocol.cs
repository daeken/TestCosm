#pragma warning disable CS1998
// ReSharper disable ConvertClosureToMethodGroup
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UseObjectOrCollectionInitializer
// ReSharper disable RedundantUsingDirective
// ReSharper disable RedundantAssignment
// ReSharper disable RedundantLambdaSignatureParentheses
// ReSharper disable UnusedParameter.Local
// ReSharper disable VariableHidesOuterVariable
using System;
using System.Numerics;
using System.Collections.Generic;
namespace NetLib.Generated;

public interface Object {
	ulong ObjectId { get; }
	Task<string[]> ListInterfaces();
	Task Release();
}
public abstract class BaseObject : ILocalObject, Object {
	protected readonly IConnection Connection;
	public ulong ObjectId { get; }
	protected BaseObject(IConnection connection) {
		Connection = connection;
		ObjectId = Connection.RegisterLocalObject(this);
	}
	public abstract Task<string[]> ListInterfaces();
	public abstract Task Release();

	public virtual async Task HandleMessage(ulong sequence, int commandNumber, Memory<byte> buf, int offset) {
		switch(commandNumber) {
			case 0: {
				var __ret = await ListInterfaces();
				if(sequence != 0) {
					buf = new byte[NetExtensions.SizeVu64((ulong) __ret.Length) + __ret.Select(_0 => NetExtensions.SizeString(_0)).Sum()];
					offset = 0;
					NetExtensions.SerializeVu64((ulong) __ret.Length, buf.Span, ref offset);
					foreach(var _3 in __ret) {
						NetExtensions.SerializeString(_3, buf.Span, ref offset);
					}
					await Connection.Respond(sequence, buf);
				}
				break;
			}
			case 1: {
				await Release();
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
				break;
			}
			default:
				throw new UnknownCommandException();
		}
	}
}
public class RemoteObject : IRemoteObject, Object {
	protected readonly IConnection Connection;
	public ulong ObjectId { get; }
	public RemoteObject(IConnection connection, ulong id) {
		Connection = connection;
		ObjectId = id;
	}
	public async Task<string[]> ListInterfaces() {
		var offset = 0;
		var buf = await Connection.Call(ObjectId, 0, Memory<byte>.Empty);
		var ret = new string[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
		for(var i0 = 0; i0 < ret.Length; ++i0) {
			ret[i0] = NetExtensions.DeserializeString(buf.Span, ref offset);
		}
		return ret;
	}
	public async Task Release() {
		await Connection.Call(ObjectId, 1, Memory<byte>.Empty);
	}
}

public interface Root : Object {
	Task<string[]> ListExtensions();
	Task Ping();
	Task<Object> GetObjectById(Uuid id);
	Task<Object> GetObjectByName(string name);
}
public abstract class BaseRoot : BaseObject, Root {
	protected BaseRoot(IConnection connection) : base(connection) {}
	public abstract Task<string[]> ListExtensions();
	public abstract Task Ping();
	public abstract Task<Object> GetObjectById(Uuid id);
	public abstract Task<Object> GetObjectByName(string name);

	public override async Task HandleMessage(ulong sequence, int commandNumber, Memory<byte> buf, int offset) {
		switch(commandNumber) {
			case 0 or 1: await base.HandleMessage(sequence, commandNumber, buf, offset); break;
			case 2: {
				var __ret = await ListExtensions();
				if(sequence != 0) {
					buf = new byte[NetExtensions.SizeVu64((ulong) __ret.Length) + __ret.Select(_0 => NetExtensions.SizeString(_0)).Sum()];
					offset = 0;
					NetExtensions.SerializeVu64((ulong) __ret.Length, buf.Span, ref offset);
					foreach(var _3 in __ret) {
						NetExtensions.SerializeString(_3, buf.Span, ref offset);
					}
					await Connection.Respond(sequence, buf);
				}
				break;
			}
			case 3: {
				await Ping();
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
				break;
			}
			case 4: {
				var Id = Uuid.Deserialize(buf.Span, ref offset);
				var __ret = await GetObjectById(Id);
				if(sequence != 0) {
					buf = new byte[NetExtensions.SizeVu64(__ret.ObjectId)];
					offset = 0;
					NetExtensions.SerializeVu64(__ret.ObjectId, buf.Span, ref offset);
					await Connection.Respond(sequence, buf);
				}
				break;
			}
			case 5: {
				var Name = NetExtensions.DeserializeString(buf.Span, ref offset);
				var __ret = await GetObjectByName(Name);
				if(sequence != 0) {
					buf = new byte[NetExtensions.SizeVu64(__ret.ObjectId)];
					offset = 0;
					NetExtensions.SerializeVu64(__ret.ObjectId, buf.Span, ref offset);
					await Connection.Respond(sequence, buf);
				}
				break;
			}
			default:
				throw new UnknownCommandException();
		}
	}
}
public class RemoteRoot : RemoteObject, Root {
	public RemoteRoot(IConnection connection, ulong id) : base(connection, id) {}
	public async Task<string[]> ListExtensions() {
		var offset = 0;
		var buf = await Connection.Call(ObjectId, 2, Memory<byte>.Empty);
		var ret = new string[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
		for(var i0 = 0; i0 < ret.Length; ++i0) {
			ret[i0] = NetExtensions.DeserializeString(buf.Span, ref offset);
		}
		return ret;
	}
	public async Task Ping() {
		await Connection.Call(ObjectId, 3, Memory<byte>.Empty);
	}
	public async Task<Object> GetObjectById(Uuid id) {
		var offset = 0;
		Memory<byte> buf = new byte[16];
		id.Serialize(buf.Span, ref offset);
		buf = await Connection.Call(ObjectId, 4, buf);
		offset = 0;
		var ret = Connection.GetObject<Object>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id => new RemoteObject(Connection, _id));
		return ret;
	}
	public async Task<Object> GetObjectByName(string name) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeString(name)];
		NetExtensions.SerializeString(name, buf.Span, ref offset);
		buf = await Connection.Call(ObjectId, 5, buf);
		offset = 0;
		var ret = Connection.GetObject<Object>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id => new RemoteObject(Connection, _id));
		return ret;
	}
}

public interface Assetdelivery : Object {
	Task SubscribeLoadAssets(Func<Asset[], Task> callback);
	Task UnsubscribeLoadAssets(Func<Asset[], Task> callback);
	Task SubscribeUnloadAssets(Func<Uuid[], Task> callback);
	Task UnsubscribeUnloadAssets(Func<Uuid[], Task> callback);
	Task<Asset> FetchAssetById(Uuid id);
	Task<Asset> FetchAssetByName(string name);
	Task<Asset[]> FetchAssetsByIds(Uuid[] ids);
	Task<Asset[]> FetchAssetsByNames(string[] names);
	Task<Uuid> GetId(string name);
}
public abstract class BaseAssetdelivery : BaseObject, Assetdelivery {
	protected BaseAssetdelivery(IConnection connection) : base(connection) {}
	public abstract Task SubscribeLoadAssets(Func<Asset[], Task> callback);
	public abstract Task UnsubscribeLoadAssets(Func<Asset[], Task> callback);
	public abstract Task SubscribeUnloadAssets(Func<Uuid[], Task> callback);
	public abstract Task UnsubscribeUnloadAssets(Func<Uuid[], Task> callback);
	public abstract Task<Asset> FetchAssetById(Uuid id);
	public abstract Task<Asset> FetchAssetByName(string name);
	public abstract Task<Asset[]> FetchAssetsByIds(Uuid[] ids);
	public abstract Task<Asset[]> FetchAssetsByNames(string[] names);
	public abstract Task<Uuid> GetId(string name);

	public override async Task HandleMessage(ulong sequence, int commandNumber, Memory<byte> buf, int offset) {
		switch(commandNumber) {
			case 0 or 1: await base.HandleMessage(sequence, commandNumber, buf, offset); break;
			case 2: {
				var Callback = Connection.GetCallback<Func<Asset[], Task>>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id =>
					async (_p0) => {
						var offset = 0;
						Memory<byte> buf = new byte[NetExtensions.SizeVu64((ulong) _p0.Length) + _p0.Select(_0 => _0.SerializedSize).Sum()];
						NetExtensions.SerializeVu64((ulong) _p0.Length, buf.Span, ref offset);
						foreach(var _4 in _p0) {
							_4.Serialize(Connection, buf.Span, ref offset);
						}
						await Connection.Call(_id, 2, buf);
						offset = 0;
					}
				);
				await SubscribeLoadAssets(Callback);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
				break;
			}
			case 3: {
				var Callback = Connection.GetCallback<Func<Asset[], Task>>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id =>
					async (_p0) => {
						var offset = 0;
						Memory<byte> buf = new byte[NetExtensions.SizeVu64((ulong) _p0.Length) + _p0.Select(_0 => _0.SerializedSize).Sum()];
						NetExtensions.SerializeVu64((ulong) _p0.Length, buf.Span, ref offset);
						foreach(var _4 in _p0) {
							_4.Serialize(Connection, buf.Span, ref offset);
						}
						await Connection.Call(_id, 2, buf);
						offset = 0;
					}
				);
				await UnsubscribeLoadAssets(Callback);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
				break;
			}
			case 4: {
				var Callback = Connection.GetCallback<Func<Uuid[], Task>>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id =>
					async (_p0) => {
						var offset = 0;
						Memory<byte> buf = new byte[NetExtensions.SizeVu64((ulong) _p0.Length) + _p0.Select(_0 => 16).Sum()];
						NetExtensions.SerializeVu64((ulong) _p0.Length, buf.Span, ref offset);
						foreach(var _4 in _p0) {
							_4.Serialize(buf.Span, ref offset);
						}
						await Connection.Call(_id, 2, buf);
						offset = 0;
					}
				);
				await SubscribeUnloadAssets(Callback);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
				break;
			}
			case 5: {
				var Callback = Connection.GetCallback<Func<Uuid[], Task>>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id =>
					async (_p0) => {
						var offset = 0;
						Memory<byte> buf = new byte[NetExtensions.SizeVu64((ulong) _p0.Length) + _p0.Select(_0 => 16).Sum()];
						NetExtensions.SerializeVu64((ulong) _p0.Length, buf.Span, ref offset);
						foreach(var _4 in _p0) {
							_4.Serialize(buf.Span, ref offset);
						}
						await Connection.Call(_id, 2, buf);
						offset = 0;
					}
				);
				await UnsubscribeUnloadAssets(Callback);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
				break;
			}
			case 6: {
				var Id = Uuid.Deserialize(buf.Span, ref offset);
				var __ret = await FetchAssetById(Id);
				if(sequence != 0) {
					buf = new byte[__ret.SerializedSize];
					offset = 0;
					__ret.Serialize(Connection, buf.Span, ref offset);
					await Connection.Respond(sequence, buf);
				}
				break;
			}
			case 7: {
				var Name = NetExtensions.DeserializeString(buf.Span, ref offset);
				var __ret = await FetchAssetByName(Name);
				if(sequence != 0) {
					buf = new byte[__ret.SerializedSize];
					offset = 0;
					__ret.Serialize(Connection, buf.Span, ref offset);
					await Connection.Respond(sequence, buf);
				}
				break;
			}
			case 8: {
				var Ids = new Uuid[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
				for(var i0 = 0; i0 < Ids.Length; ++i0) {
					Ids[i0] = Uuid.Deserialize(buf.Span, ref offset);
				}
				var __ret = await FetchAssetsByIds(Ids);
				if(sequence != 0) {
					buf = new byte[NetExtensions.SizeVu64((ulong) __ret.Length) + __ret.Select(_0 => _0.SerializedSize).Sum()];
					offset = 0;
					NetExtensions.SerializeVu64((ulong) __ret.Length, buf.Span, ref offset);
					foreach(var _3 in __ret) {
						_3.Serialize(Connection, buf.Span, ref offset);
					}
					await Connection.Respond(sequence, buf);
				}
				break;
			}
			case 9: {
				var Names = new string[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
				for(var i0 = 0; i0 < Names.Length; ++i0) {
					Names[i0] = NetExtensions.DeserializeString(buf.Span, ref offset);
				}
				var __ret = await FetchAssetsByNames(Names);
				if(sequence != 0) {
					buf = new byte[NetExtensions.SizeVu64((ulong) __ret.Length) + __ret.Select(_0 => _0.SerializedSize).Sum()];
					offset = 0;
					NetExtensions.SerializeVu64((ulong) __ret.Length, buf.Span, ref offset);
					foreach(var _3 in __ret) {
						_3.Serialize(Connection, buf.Span, ref offset);
					}
					await Connection.Respond(sequence, buf);
				}
				break;
			}
			case 10: {
				var Name = NetExtensions.DeserializeString(buf.Span, ref offset);
				var __ret = await GetId(Name);
				if(sequence != 0) {
					buf = new byte[16];
					offset = 0;
					__ret.Serialize(buf.Span, ref offset);
					await Connection.Respond(sequence, buf);
				}
				break;
			}
			default:
				throw new UnknownCommandException();
		}
	}
}
public class RemoteAssetdelivery : RemoteObject, Assetdelivery {
	public RemoteAssetdelivery(IConnection connection, ulong id) : base(connection, id) {}
	public async Task SubscribeLoadAssets(Func<Asset[], Task> callback) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeVu64(Connection.GetCallbackId(callback))];
		NetExtensions.SerializeVu64(Connection.GetCallbackId(callback, () =>
			async (sequence, buf) => {
				var offset = 0;
				var _p0 = new Asset[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
				for(var i0 = 0; i0 < _p0.Length; ++i0) {
					_p0[i0] = Asset.Deserialize(Connection, buf.Span, ref offset);
				}
				await callback(_p0);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
			}
		), buf.Span, ref offset);
		await Connection.Call(ObjectId, 2, buf);
		offset = 0;
	}
	public async Task UnsubscribeLoadAssets(Func<Asset[], Task> callback) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeVu64(Connection.GetCallbackId(callback))];
		NetExtensions.SerializeVu64(Connection.GetCallbackId(callback, () =>
			async (sequence, buf) => {
				var offset = 0;
				var _p0 = new Asset[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
				for(var i0 = 0; i0 < _p0.Length; ++i0) {
					_p0[i0] = Asset.Deserialize(Connection, buf.Span, ref offset);
				}
				await callback(_p0);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
			}
		), buf.Span, ref offset);
		await Connection.Call(ObjectId, 3, buf);
		offset = 0;
	}
	public async Task SubscribeUnloadAssets(Func<Uuid[], Task> callback) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeVu64(Connection.GetCallbackId(callback))];
		NetExtensions.SerializeVu64(Connection.GetCallbackId(callback, () =>
			async (sequence, buf) => {
				var offset = 0;
				var _p0 = new Uuid[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
				for(var i0 = 0; i0 < _p0.Length; ++i0) {
					_p0[i0] = Uuid.Deserialize(buf.Span, ref offset);
				}
				await callback(_p0);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
			}
		), buf.Span, ref offset);
		await Connection.Call(ObjectId, 4, buf);
		offset = 0;
	}
	public async Task UnsubscribeUnloadAssets(Func<Uuid[], Task> callback) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeVu64(Connection.GetCallbackId(callback))];
		NetExtensions.SerializeVu64(Connection.GetCallbackId(callback, () =>
			async (sequence, buf) => {
				var offset = 0;
				var _p0 = new Uuid[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
				for(var i0 = 0; i0 < _p0.Length; ++i0) {
					_p0[i0] = Uuid.Deserialize(buf.Span, ref offset);
				}
				await callback(_p0);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
			}
		), buf.Span, ref offset);
		await Connection.Call(ObjectId, 5, buf);
		offset = 0;
	}
	public async Task<Asset> FetchAssetById(Uuid id) {
		var offset = 0;
		Memory<byte> buf = new byte[16];
		id.Serialize(buf.Span, ref offset);
		buf = await Connection.Call(ObjectId, 6, buf);
		offset = 0;
		var ret = Asset.Deserialize(Connection, buf.Span, ref offset);
		return ret;
	}
	public async Task<Asset> FetchAssetByName(string name) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeString(name)];
		NetExtensions.SerializeString(name, buf.Span, ref offset);
		buf = await Connection.Call(ObjectId, 7, buf);
		offset = 0;
		var ret = Asset.Deserialize(Connection, buf.Span, ref offset);
		return ret;
	}
	public async Task<Asset[]> FetchAssetsByIds(Uuid[] ids) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeVu64((ulong) ids.Length) + ids.Select(_0 => 16).Sum()];
		NetExtensions.SerializeVu64((ulong) ids.Length, buf.Span, ref offset);
		foreach(var _0 in ids) {
			_0.Serialize(buf.Span, ref offset);
		}
		buf = await Connection.Call(ObjectId, 8, buf);
		offset = 0;
		var ret = new Asset[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
		for(var i0 = 0; i0 < ret.Length; ++i0) {
			ret[i0] = Asset.Deserialize(Connection, buf.Span, ref offset);
		}
		return ret;
	}
	public async Task<Asset[]> FetchAssetsByNames(string[] names) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeVu64((ulong) names.Length) + names.Select(_0 => NetExtensions.SizeString(_0)).Sum()];
		NetExtensions.SerializeVu64((ulong) names.Length, buf.Span, ref offset);
		foreach(var _0 in names) {
			NetExtensions.SerializeString(_0, buf.Span, ref offset);
		}
		buf = await Connection.Call(ObjectId, 9, buf);
		offset = 0;
		var ret = new Asset[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
		for(var i0 = 0; i0 < ret.Length; ++i0) {
			ret[i0] = Asset.Deserialize(Connection, buf.Span, ref offset);
		}
		return ret;
	}
	public async Task<Uuid> GetId(string name) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeString(name)];
		NetExtensions.SerializeString(name, buf.Span, ref offset);
		buf = await Connection.Call(ObjectId, 10, buf);
		offset = 0;
		var ret = Uuid.Deserialize(buf.Span, ref offset);
		return ret;
	}
}

public struct Asset {
	public Uuid Id;
	public string Name;
	public byte[] Data;

	public int SerializedSize => 16 + NetExtensions.SizeString(Name) + NetExtensions.SizeVu64((ulong) Data.Length) + Data.Select(_0 => NetExtensions.SizeU8(_0)).Sum();
	public void Serialize(IConnection connection, Span<byte> buf, ref int offset) {
		Id.Serialize(buf, ref offset);
		NetExtensions.SerializeString(Name, buf, ref offset);
		NetExtensions.SerializeVu64((ulong) Data.Length, buf, ref offset);
		foreach(var _0 in Data) {
			NetExtensions.SerializeU8(_0, buf, ref offset);
		}
	}
	public static Asset Deserialize(IConnection connection, Span<byte> buf, ref int offset) {
		var obj = new Asset();
		obj.Id = Uuid.Deserialize(buf, ref offset);
		obj.Name = NetExtensions.DeserializeString(buf, ref offset);
		obj.Data = new byte[(int) NetExtensions.DeserializeVu64(buf, ref offset)];
		for(var i0 = 0; i0 < obj.Data.Length; ++i0) {
			obj.Data[i0] = NetExtensions.DeserializeU8(buf, ref offset);
		}
		return obj;
	}
}

public interface World : Object {
	Task SubscribeAddEntities(Func<EntityInfo[], Task> callback);
	Task UnsubscribeAddEntities(Func<EntityInfo[], Task> callback);
	Task SubscribeUpdateEntities(Func<EntityInfo[], Task> callback);
	Task UnsubscribeUpdateEntities(Func<EntityInfo[], Task> callback);
	Task SubscribeRemoveEntities(Func<Entity[], Task> callback);
	Task UnsubscribeRemoveEntities(Func<Entity[], Task> callback);
}
public abstract class BaseWorld : BaseObject, World {
	protected BaseWorld(IConnection connection) : base(connection) {}
	public abstract Task SubscribeAddEntities(Func<EntityInfo[], Task> callback);
	public abstract Task UnsubscribeAddEntities(Func<EntityInfo[], Task> callback);
	public abstract Task SubscribeUpdateEntities(Func<EntityInfo[], Task> callback);
	public abstract Task UnsubscribeUpdateEntities(Func<EntityInfo[], Task> callback);
	public abstract Task SubscribeRemoveEntities(Func<Entity[], Task> callback);
	public abstract Task UnsubscribeRemoveEntities(Func<Entity[], Task> callback);

	public override async Task HandleMessage(ulong sequence, int commandNumber, Memory<byte> buf, int offset) {
		switch(commandNumber) {
			case 0 or 1: await base.HandleMessage(sequence, commandNumber, buf, offset); break;
			case 2: {
				var Callback = Connection.GetCallback<Func<EntityInfo[], Task>>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id =>
					async (_p0) => {
						var offset = 0;
						Memory<byte> buf = new byte[NetExtensions.SizeVu64((ulong) _p0.Length) + _p0.Select(_0 => _0.SerializedSize).Sum()];
						NetExtensions.SerializeVu64((ulong) _p0.Length, buf.Span, ref offset);
						foreach(var _4 in _p0) {
							_4.Serialize(Connection, buf.Span, ref offset);
						}
						await Connection.Call(_id, 2, buf);
						offset = 0;
					}
				);
				await SubscribeAddEntities(Callback);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
				break;
			}
			case 3: {
				var Callback = Connection.GetCallback<Func<EntityInfo[], Task>>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id =>
					async (_p0) => {
						var offset = 0;
						Memory<byte> buf = new byte[NetExtensions.SizeVu64((ulong) _p0.Length) + _p0.Select(_0 => _0.SerializedSize).Sum()];
						NetExtensions.SerializeVu64((ulong) _p0.Length, buf.Span, ref offset);
						foreach(var _4 in _p0) {
							_4.Serialize(Connection, buf.Span, ref offset);
						}
						await Connection.Call(_id, 2, buf);
						offset = 0;
					}
				);
				await UnsubscribeAddEntities(Callback);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
				break;
			}
			case 4: {
				var Callback = Connection.GetCallback<Func<EntityInfo[], Task>>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id =>
					async (_p0) => {
						var offset = 0;
						Memory<byte> buf = new byte[NetExtensions.SizeVu64((ulong) _p0.Length) + _p0.Select(_0 => _0.SerializedSize).Sum()];
						NetExtensions.SerializeVu64((ulong) _p0.Length, buf.Span, ref offset);
						foreach(var _4 in _p0) {
							_4.Serialize(Connection, buf.Span, ref offset);
						}
						await Connection.Call(_id, 2, buf);
						offset = 0;
					}
				);
				await SubscribeUpdateEntities(Callback);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
				break;
			}
			case 5: {
				var Callback = Connection.GetCallback<Func<EntityInfo[], Task>>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id =>
					async (_p0) => {
						var offset = 0;
						Memory<byte> buf = new byte[NetExtensions.SizeVu64((ulong) _p0.Length) + _p0.Select(_0 => _0.SerializedSize).Sum()];
						NetExtensions.SerializeVu64((ulong) _p0.Length, buf.Span, ref offset);
						foreach(var _4 in _p0) {
							_4.Serialize(Connection, buf.Span, ref offset);
						}
						await Connection.Call(_id, 2, buf);
						offset = 0;
					}
				);
				await UnsubscribeUpdateEntities(Callback);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
				break;
			}
			case 6: {
				var Callback = Connection.GetCallback<Func<Entity[], Task>>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id =>
					async (_p0) => {
						var offset = 0;
						Memory<byte> buf = new byte[NetExtensions.SizeVu64((ulong) _p0.Length) + _p0.Select(_0 => NetExtensions.SizeVu64(_0.ObjectId)).Sum()];
						NetExtensions.SerializeVu64((ulong) _p0.Length, buf.Span, ref offset);
						foreach(var _4 in _p0) {
							NetExtensions.SerializeVu64(_4.ObjectId, buf.Span, ref offset);
						}
						await Connection.Call(_id, 2, buf);
						offset = 0;
					}
				);
				await SubscribeRemoveEntities(Callback);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
				break;
			}
			case 7: {
				var Callback = Connection.GetCallback<Func<Entity[], Task>>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id =>
					async (_p0) => {
						var offset = 0;
						Memory<byte> buf = new byte[NetExtensions.SizeVu64((ulong) _p0.Length) + _p0.Select(_0 => NetExtensions.SizeVu64(_0.ObjectId)).Sum()];
						NetExtensions.SerializeVu64((ulong) _p0.Length, buf.Span, ref offset);
						foreach(var _4 in _p0) {
							NetExtensions.SerializeVu64(_4.ObjectId, buf.Span, ref offset);
						}
						await Connection.Call(_id, 2, buf);
						offset = 0;
					}
				);
				await UnsubscribeRemoveEntities(Callback);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
				break;
			}
			default:
				throw new UnknownCommandException();
		}
	}
}
public class RemoteWorld : RemoteObject, World {
	public RemoteWorld(IConnection connection, ulong id) : base(connection, id) {}
	public async Task SubscribeAddEntities(Func<EntityInfo[], Task> callback) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeVu64(Connection.GetCallbackId(callback))];
		NetExtensions.SerializeVu64(Connection.GetCallbackId(callback, () =>
			async (sequence, buf) => {
				var offset = 0;
				var _p0 = new EntityInfo[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
				for(var i0 = 0; i0 < _p0.Length; ++i0) {
					_p0[i0] = EntityInfo.Deserialize(Connection, buf.Span, ref offset);
				}
				await callback(_p0);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
			}
		), buf.Span, ref offset);
		await Connection.Call(ObjectId, 2, buf);
		offset = 0;
	}
	public async Task UnsubscribeAddEntities(Func<EntityInfo[], Task> callback) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeVu64(Connection.GetCallbackId(callback))];
		NetExtensions.SerializeVu64(Connection.GetCallbackId(callback, () =>
			async (sequence, buf) => {
				var offset = 0;
				var _p0 = new EntityInfo[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
				for(var i0 = 0; i0 < _p0.Length; ++i0) {
					_p0[i0] = EntityInfo.Deserialize(Connection, buf.Span, ref offset);
				}
				await callback(_p0);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
			}
		), buf.Span, ref offset);
		await Connection.Call(ObjectId, 3, buf);
		offset = 0;
	}
	public async Task SubscribeUpdateEntities(Func<EntityInfo[], Task> callback) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeVu64(Connection.GetCallbackId(callback))];
		NetExtensions.SerializeVu64(Connection.GetCallbackId(callback, () =>
			async (sequence, buf) => {
				var offset = 0;
				var _p0 = new EntityInfo[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
				for(var i0 = 0; i0 < _p0.Length; ++i0) {
					_p0[i0] = EntityInfo.Deserialize(Connection, buf.Span, ref offset);
				}
				await callback(_p0);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
			}
		), buf.Span, ref offset);
		await Connection.Call(ObjectId, 4, buf);
		offset = 0;
	}
	public async Task UnsubscribeUpdateEntities(Func<EntityInfo[], Task> callback) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeVu64(Connection.GetCallbackId(callback))];
		NetExtensions.SerializeVu64(Connection.GetCallbackId(callback, () =>
			async (sequence, buf) => {
				var offset = 0;
				var _p0 = new EntityInfo[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
				for(var i0 = 0; i0 < _p0.Length; ++i0) {
					_p0[i0] = EntityInfo.Deserialize(Connection, buf.Span, ref offset);
				}
				await callback(_p0);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
			}
		), buf.Span, ref offset);
		await Connection.Call(ObjectId, 5, buf);
		offset = 0;
	}
	public async Task SubscribeRemoveEntities(Func<Entity[], Task> callback) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeVu64(Connection.GetCallbackId(callback))];
		NetExtensions.SerializeVu64(Connection.GetCallbackId(callback, () =>
			async (sequence, buf) => {
				var offset = 0;
				var _p0 = new Entity[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
				for(var i0 = 0; i0 < _p0.Length; ++i0) {
					_p0[i0] = Connection.GetObject<Entity>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id => new RemoteEntity(Connection, _id));
				}
				await callback(_p0);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
			}
		), buf.Span, ref offset);
		await Connection.Call(ObjectId, 6, buf);
		offset = 0;
	}
	public async Task UnsubscribeRemoveEntities(Func<Entity[], Task> callback) {
		var offset = 0;
		Memory<byte> buf = new byte[NetExtensions.SizeVu64(Connection.GetCallbackId(callback))];
		NetExtensions.SerializeVu64(Connection.GetCallbackId(callback, () =>
			async (sequence, buf) => {
				var offset = 0;
				var _p0 = new Entity[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];
				for(var i0 = 0; i0 < _p0.Length; ++i0) {
					_p0[i0] = Connection.GetObject<Entity>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id => new RemoteEntity(Connection, _id));
				}
				await callback(_p0);
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
			}
		), buf.Span, ref offset);
		await Connection.Call(ObjectId, 7, buf);
		offset = 0;
	}
}

public interface Entity : Object {
	Task Interact();
}
public abstract class BaseEntity : BaseObject, Entity {
	protected BaseEntity(IConnection connection) : base(connection) {}
	public abstract Task Interact();

	public override async Task HandleMessage(ulong sequence, int commandNumber, Memory<byte> buf, int offset) {
		switch(commandNumber) {
			case 0 or 1: await base.HandleMessage(sequence, commandNumber, buf, offset); break;
			case 2: {
				await Interact();
				if(sequence != 0) {
					await Connection.Respond(sequence, Memory<byte>.Empty);
				}
				break;
			}
			default:
				throw new UnknownCommandException();
		}
	}
}
public class RemoteEntity : RemoteObject, Entity {
	public RemoteEntity(IConnection connection, ulong id) : base(connection, id) {}
	public async Task Interact() {
		await Connection.Call(ObjectId, 2, Memory<byte>.Empty);
	}
}

public struct EntityInfo {
	public Uuid AssetId;
	public Entity Entity;
	public Matrix4x4 Transformation;
	public EntityFlags Flags;

	public int SerializedSize => 16 + NetExtensions.SizeVu64(Entity.ObjectId) + NetExtensions.SizeMatrix4x4(Transformation) + NetExtensions.SizeVu64((ulong) Flags);
	public void Serialize(IConnection connection, Span<byte> buf, ref int offset) {
		AssetId.Serialize(buf, ref offset);
		NetExtensions.SerializeVu64(Entity.ObjectId, buf, ref offset);
		NetExtensions.SerializeMatrix4x4(Transformation, buf, ref offset);
		NetExtensions.SerializeVu64((ulong) Flags, buf, ref offset);
	}
	public static EntityInfo Deserialize(IConnection connection, Span<byte> buf, ref int offset) {
		var obj = new EntityInfo();
		obj.AssetId = Uuid.Deserialize(buf, ref offset);
		obj.Entity = connection.GetObject<Entity>(NetExtensions.DeserializeVu64(buf, ref offset), _id => new RemoteEntity(connection, _id));
		obj.Transformation = NetExtensions.DeserializeMatrix4x4(buf, ref offset);
		obj.Flags = (EntityFlags) NetExtensions.DeserializeVu64(buf, ref offset);
		return obj;
	}
}

public enum EntityFlags : ulong {
	None = 0,
	Interactable = 1,
	Collidable = 2,
}
