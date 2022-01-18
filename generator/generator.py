from json import load

spec = load(file('HIDL/json/hypercosm.json'))

import pprint
pprint.pprint(spec)

namespaces = {}

namespace = []

def getNs():
	nsn = '.'.join(map(title, namespace))
	if nsn not in namespaces:
		namespaces[nsn] = dict(
			structs={}, 
			interfaces={}, 
			enums={}
		)
	return namespaces[nsn]

def title(x):
	if '_' in x:
		return ''.join(map(title, x.split('_')))
	elif len(x) == 0:
		return x
	return x[0].upper() + x[1:]

def named(x):
	if '_' in x:
		s = x.split('_')
		return s[0] + ''.join(map(title, s[1:]))
	return x

def parseInterface(elem): # TODO: Handle inheritance
	name = elem['name']
	
	events = elem['events']
	methods = elem['methods']

	emethods = []
	for evt in events:
		callbackType = dict(Callback=dict(
			args=evt['args'], 
			ret=evt['ret']
		))

		cargs = [dict(name='callback', ty=callbackType)]

		emethods.append(dict(name='Subscribe' + title(evt['name']), args=cargs, ret=None))
		emethods.append(dict(name='Unsubscribe' + title(evt['name']), args=cargs, ret=None))

	methods = emethods + methods

	ns = getNs()
	ns['interfaces'][name] = {'methods' : methods}

def parseExtension(elem):
	namespace.append(elem['name'])
	map(parseInterface, elem['interfaces'])
	map(parseTypedef, elem['types'])
	if 'extensions' in elem:
		map(parseExtension, elem['extensions'])
	namespace.pop()

def parseTypedef(elem):
	type = elem['kind']
	if 'Struct' in type:
		parseStruct(elem['name'], type['Struct'])
	elif 'Enum' in type:
		parseEnum(elem['name'], type['Enum'])

def parseStruct(name, elem):
	def proc(elem):
		return elem['ty'], elem['name']
	ns = getNs()
	ns['structs'][name] = map(proc, elem['fields'])

def parseEnum(name, elem):
	def proc(elem):
		return elem['name'], elem['value']
	ns = getNs()
	ns['enums'][name] = (False, dict(Primitive=elem['backing'].lower()), map(proc, elem['fields']))

parseExtension(spec)

btypes = dict(
	void='void', 
	string='string', 
	uuid='Uuid', 
	u8='byte', 
	i8='sbyte', 
	u16='ushort', 
	i16='short', 
	u32='uint', 
	i32='int', 
	vu32='uint', 
	vi32='int', 
	u64='ulong', 
	i64='long', 
	vu64='ulong', 
	vi64='long', 
	matrix4x4='Matrix4x4',
)

vtypes = (
	'vu32', 'vi32', 
	'vu64', 'vi64', 
)

def typed(type, isRet=False):
	if isRet:
		if type is None:
			return 'Task'
		else:
			return 'Task<%s>' % typed(type)
	if type is None:
		return 'void'
	elif 'IntType' in type:
		return typed(dict(Primitive=type['IntType']), isRet)
	elif 'Array' in type:
		return '%s[]' % typed(type['Array'])
	elif 'Callback' in type:
		type = type['Callback']
		ret = typed(type['ret'], True)
		if len(type['args']):
			return 'Func<%s, %s>' % (', '.join(typed(arg['ty']) for arg in type['args']), ret)
		else:
			return 'Func<%s>' % ret
	elif 'Primitive' in type and type['Primitive'].lower() in btypes:
		return btypes[type['Primitive'].lower()]
	elif 'Primitive' in type and type['Primitive'] == 'Object':
		return 'Object'
	elif 'Custom' in type:
		return title(type['Custom'])
	print type
	assert len(type) == 1
	return title(type[0])

with file('../NetLib/Generated/Protocol.cs', 'w') as fp:
	print >>fp, '''#pragma warning disable CS1998
// ReSharper disable ConvertClosureToMethodGroup
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UseObjectOrCollectionInitializer
// ReSharper disable RedundantUsingDirective
// ReSharper disable RedundantAssignment
// ReSharper disable RedundantLambdaSignatureParentheses
// ReSharper disable UnusedParameter.Local
// ReSharper disable VariableHidesOuterVariable'''
	print >>fp, 'using System;'
	print >>fp, 'using System.Numerics;'
	print >>fp, 'using System.Collections.Generic;'
	print >>fp, 'namespace NetLib.Generated;'
	for nsn, ns in namespaces.items():
		def genDeserialize(type, name, depth = 0):
			if 'IntType' in type:
				return genDeserialize(dict(Primitive=type['IntType']), name, depth)
			ws = '\t\t' + '\t' * depth
			if 'Array' in type:
				print >>fp, ws + '%s = new %s[(int) NetExtensions.DeserializeVu64(buf, ref offset)];' % (name, typed(type['Array']))
				print >>fp, ws + 'for(var i%i = 0; i%i < %s.Length; ++i%i) {' % (depth, depth, name, depth)
				genDeserialize(type['Array'], '%s[i%i]' % (name, depth), depth + 1)
				print >>fp, ws + '}'
			elif 'Primitive' in type and type['Primitive'].lower() in btypes and type['Primitive'] != 'Uuid':
				print >>fp, ws + '%s = NetExtensions.Deserialize%s(buf, ref offset);' % (name, title(type['Primitive'].lower()))
			elif 'Custom' in type and type['Custom'] in ns['enums']:
				print >>fp, ws + '%s = (%s) NetExtensions.Deserialize%s(buf, ref offset);' % (name, title(type['Custom']), title(ns['enums'][type['Custom']][1]['Primitive'].lower()))
			elif 'Primitive' in type and type['Primitive'] == 'Object':
				print >>fp, ws + '%s = connection.GetObject<Object>(NetExtensions.DeserializeVu64(buf, ref offset), _id => new RemoteObject(connection, _id));' % name
			elif 'Custom' in type and type['Custom'] in ns['interfaces']:
				print >>fp, ws + '%s = connection.GetObject<%s>(NetExtensions.DeserializeVu64(buf, ref offset), _id => new Remote%s(connection, _id));' % (name, title(type['Custom']), title(type['Custom']))
			elif 'Primitive' in type and type['Primitive'] == 'Uuid':
				print >>fp, ws + '%s = Uuid.Deserialize(buf, ref offset);' % name
			else:
				print >>fp, ws + '%s = %s.Deserialize(connection, buf, ref offset);' % (name, title(type[0]))
		def genMsgDeserialize(type, name, depth = 0, depthOffset = 0):
			if 'IntType' in type:
				return genMsgDeserialize(dict(Primitive=type['IntType']), name, depth, depthOffset)
			ws = '\t\t' + '\t' * (depth + depthOffset)
			var = 'var ' if depth == 0 else ''
			if 'Array' in type:
				print >>fp, ws + '%s%s = new %s[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];' % (var, name, typed(type['Array']))
				print >>fp, ws + 'for(var i%i = 0; i%i < %s.Length; ++i%i) {' % (depth, depth, name, depth)
				genMsgDeserialize(type['Array'], '%s[i%i]' % (name, depth), depth + 1, depthOffset)
				print >>fp, ws + '}'
			elif 'Primitive' in type and type['Primitive'].lower() in btypes and type['Primitive'] != 'Uuid':
				print >>fp, ws + '%s%s = NetExtensions.Deserialize%s(buf.Span, ref offset);' % (var, name, title(type['Primitive'].lower()))
			elif 'Custom' in type and type['Custom'] in ns['enums']:
				print >>fp, ws + '%s%s = (%s) NetExtensions.Deserialize%s(buf.Span, ref offset);' % (var, name, title(type['Custom']), title(ns['enums'][type['Custom']][1]['Primitive'].lower()))
			elif 'Primitive' in type and type['Primitive'] == 'Object':
				print >>fp, ws + '%s%s = Connection.GetObject<Object>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id => new RemoteObject(Connection, _id));' % (var, name)
			elif 'Custom' in type and type['Custom'] in ns['interfaces']:
				print >>fp, ws + '%s%s = Connection.GetObject<%s>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id => new Remote%s(Connection, _id));' % (var, name, title(type['Custom']), title(type['Custom']))
			elif 'Callback' in type:
				print >>fp, ws + '%s%s = Connection.GetCallback<%s>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id =>' % (var, name, typed(type))
				type = type['Callback']
				print >>fp, ws + '\tasync (%s) => {' % ', '.join(named(arg['name']) for arg in type['args'])
				if len(type['args']) or type['ret'] is not None:
					print >>fp, ws + '\t\tvar offset = 0;'
				if len(type['args']) == 0:
					print >>fp, ws + '\t\t%sawait Connection.Call(_id, 2, Memory<byte>.Empty);' % ('var buf = ' if type['ret'] is not None else '')
				else:
					print >>fp, ws + '\t\tMemory<byte> buf = new byte[%s];' % ' + '.join(genMsgSize(arg['ty'], named(arg['name'])) for arg in type['args'])
					for arg in type['args']:
						genMsgSerialize(arg['ty'], named(arg['name']), 4)
					print >>fp, ws + '\t\t%sawait Connection.Call(_id, 2, buf);' % ('buf = ' if type['ret'] is not None else '')
					print >>fp, ws + '\t\toffset = 0;'
				if type['ret'] is not None:
					genMsgDeserialize(type['ret'], 'ret', depth + 1, depthOffset)
					print >>fp, ws + '\t\treturn ret;'
				print >>fp, ws + '\t}'
				print >>fp, ws + ');'
			elif 'Primitive' in type and type['Primitive'] == 'Uuid':
				print >>fp, ws + '%s%s = Uuid.Deserialize(buf.Span, ref offset);' % (var, name)
			elif 'Custom' in type:
				print >>fp, ws + '%s%s = %s.Deserialize(Connection, buf.Span, ref offset);' % (var, name, title(type['Custom']))
			else:
				print type
				assert False
		def genSerialize(type, value, depth = 0):
			if 'IntType' in type:
				return genSerialize(dict(Primitive=type['IntType']), value, depth)
			ws = '\t\t' + '\t' * depth
			if 'Array' in type:
				print >>fp, ws + 'NetExtensions.SerializeVu64((ulong) %s.Length, buf, ref offset);' % value
				print >>fp, ws + 'foreach(var _%i in %s) {' % (depth, value)
				genSerialize(type['Array'], '_%i' % depth, depth+1)
				print >>fp, ws + '}'
			elif 'Primitive' in type and type['Primitive'].lower() in btypes and type['Primitive'] != 'Uuid':
				print >>fp, ws + 'NetExtensions.Serialize%s(%s, buf, ref offset);' % (title(type['Primitive'].lower()), value)
			elif 'Custom' in type and type['Custom'] in ns['enums']:
				genSerialize(ns['enums'][type['Custom']][1], '(%s) %s' % (typed(ns['enums'][type['Custom']][1]), value), depth)
			elif ('Primitive' in type and type['Primitive'] == 'Object') or ('Custom' in type and type['Custom'] in ns['interfaces']):
				genSerialize(dict(Primitive='vu64'), '%s.ObjectId' % value, depth)
			elif 'Primitive' in type and type['Primitive'] == 'Uuid':
				print >>fp, ws + '%s.Serialize(buf, ref offset);' % value
			elif 'Callback' in type:
				print >>fp, ws + 'NetExtensions.SerializeVu64(connection.GetCallbackId(%s), buf, ref offset);' % value
			else:
				print >>fp, ws + '%s.Serialize(connection, buf, ref offset);' % value
		def genMsgSerialize(type, value, depth = 0):
			if 'IntType' in type:
				return genMsgSerialize(dict(Primitive=type['IntType']), value, depth)
			ws = '\t\t' + '\t' * depth
			if 'Array' in type:
				print >>fp, ws + 'NetExtensions.SerializeVu64((ulong) %s.Length, buf.Span, ref offset);' % value
				print >>fp, ws + 'foreach(var _%i in %s) {' % (depth, value)
				genMsgSerialize(type['Array'], '_%i' % depth, depth+1)
				print >>fp, ws + '}'
			elif 'Primitive' in type and type['Primitive'].lower() in btypes and type['Primitive'] != 'Uuid':
				print >>fp, ws + 'NetExtensions.Serialize%s(%s, buf.Span, ref offset);' % (title(type['Primitive'].lower()), value)
			elif 'Custom' in type and type['Custom'] in ns['enums']:
				genMsgSerialize(ns['enums'][type['Custom']][1], '(%s) %s' % (typed(ns['enums'][type['Custom']][1]), value), depth)
			elif ('Primitive' in type and type['Primitive'] == 'Object') or ('Custom' in type and type['Custom'] in ns['interfaces']):
				genMsgSerialize(dict(Primitive='vu64'), '%s.ObjectId' % value, depth)
			elif 'Primitive' in type and type['Primitive'] == 'Uuid':
				print >>fp, ws + '%s.Serialize(buf.Span, ref offset);' % value
			elif 'Callback' in type:
				type = type['Callback']
				print >>fp, ws + 'NetExtensions.SerializeVu64(Connection.GetCallbackId(%s, () =>' % value
				print >>fp, ws + '\tasync (sequence, buf) => {'
				if len(type['args']) or type['ret'] is not None:
					print >>fp, ws + '\t\tvar offset = 0;'
				for arg in type['args']:
					genMsgDeserialize(arg['ty'], named(arg['name']), depthOffset=2)
				if type['ret'] is None:
					ret = ''
				else:
					ret = 'var __ret = '
				print >>fp, ws + '\t\t%sawait %s(%s);' % (ret, value, ', '.join(named(arg['name']) for arg in type['args']))
				print >>fp, ws + '\t\tif(sequence != 0) {'
				if type['ret'] is None:
					print >>fp, ws + '\t\t\tawait Connection.Respond(sequence, Memory<byte>.Empty);'
				else:
					print >>fp, ws + '\t\t\tbuf = new byte[%s];' % genMsgSize(type['ret'], '__ret')
					print >>fp, ws + '\t\t\toffset = 0;'
					genMsgSerialize(type['ret'], '__ret', depth=3)
					print >>fp, ws + '\t\t\tawait Connection.Respond(sequence, buf);'
				print >>fp, ws + '\t\t}'
				print >>fp, ws + '\t}'
				print >>fp, ws + '), buf.Span, ref offset);'
			else:
				print >>fp, ws + '%s.Serialize(Connection, buf.Span, ref offset);' % value
		def genSize(type, value, depth = 0):
			if 'IntType' in type:
				return genSize(dict(Primitive=type['IntType']), value, depth)
			if 'Array' in type:
				return 'NetExtensions.SizeVu64((ulong) %s.Length) + %s.Select(_%i => %s).Sum()' % (value, value, depth, genSize(type['Array'], '_%i' % depth, depth + 1))
			elif 'Primitive' in type and type['Primitive'] == 'Uuid':
				return '16'
			elif 'Primitive' in type and type['Primitive'].lower() in btypes:
				return 'NetExtensions.Size%s(%s)' % (title(type['Primitive'].lower()), value)
			elif 'Custom' in type and type['Custom'] in ns['enums']:
				return genSize(ns['enums'][type['Custom']][1], '(%s) %s' % (typed(ns['enums'][type['Custom']][1]), value), depth)
			elif ('Primitive' in type and type['Primitive'] == 'Object') or ('Custom' in type and type['Custom'] in ns['interfaces']):
				return 'NetExtensions.SizeVu64(%s.ObjectId)' % value
			elif 'Callback' in type:
				return 'NetExtensions.SizeVu64(connection.GetCallbackId(%s))' % value
			else:
				return '%s.SerializedSize' % value
		def genMsgSize(type, value, depth = 0):
			if 'Callback' in type:
				return 'NetExtensions.SizeVu64(Connection.GetCallbackId(%s))' % value
			return genSize(type, value, depth)

		for name, iface in ns['interfaces'].items():
			isObject = nsn == 'Hypercosm' and name == 'Object'
			print >>fp
			print >>fp, 'public interface %s%s {' % (title(name), ' : Object' if not isObject else '')
			if isObject:
				print >>fp, '\tulong ObjectId { get; }'
			for method in iface['methods']:
				print >>fp, '\t%s %s(%s);' % (typed(method['ret'], isRet=True), title(method['name']), ', '.join('%s %s' % (typed(arg['ty']), named(arg['name'])) for arg in method['args']))
			print >>fp, '}'
			print >>fp, 'public abstract class Base%s : %s%s {' % (title(name), 'BaseObject, ' if not isObject else 'ILocalObject, ', title(name))
			if isObject:
				print >>fp, '\tprotected readonly IConnection Connection;'
				print >>fp, '\tpublic ulong ObjectId { get; }'
				print >>fp, '\tprotected BaseObject(IConnection connection) {'
				print >>fp, '\t\tConnection = connection;'
				print >>fp, '\t\tObjectId = Connection.RegisterLocalObject(this);'
				print >>fp, '\t}'
			else:
				print >>fp, '\tprotected Base%s(IConnection connection) : base(connection) {}' % title(name)
			for method in iface['methods']:
				print >>fp, '\tpublic abstract %s %s(%s);' % (typed(method['ret'], isRet=True), title(method['name']), ', '.join('%s %s' % (typed(arg['ty']), named(arg['name'])) for arg in method['args']))
			print >>fp
			print >>fp, '\tpublic %s async Task HandleMessage(ulong sequence, int commandNumber, Memory<byte> buf, int offset) {' % ('override' if not isObject else 'virtual')
			print >>fp, '\t\tswitch(commandNumber) {'
			cmdNum = 0
			if not isObject:
				cmdNum = 2
				print >>fp, '\t\t\tcase 0 or 1: await base.HandleMessage(sequence, commandNumber, buf, offset); break;'
			for method in iface['methods']:
				print >>fp, '\t\t\tcase %i: {' % cmdNum
				for arg in method['args']:
					genMsgDeserialize(arg['ty'], named(arg['name']), depthOffset=2)
				if method['ret'] is None:
					ret = ''
				else:
					ret = 'var __ret = '
				print >>fp, '\t\t\t\t%sawait %s(%s);' % (ret, title(method['name']), ', '.join(named(arg['name']) for arg in method['args']))
				print >>fp, '\t\t\t\tif(sequence != 0) {'
				if method['ret'] is None:
					print >>fp, '\t\t\t\t\tawait Connection.Respond(sequence, Memory<byte>.Empty);'
				else:
					print >>fp, '\t\t\t\t\tbuf = new byte[%s];' % genMsgSize(method['ret'], '__ret')
					print >>fp, '\t\t\t\t\toffset = 0;'
					genMsgSerialize(method['ret'], '__ret', depth=3)
					print >>fp, '\t\t\t\t\tawait Connection.Respond(sequence, buf);'
				print >>fp, '\t\t\t\t}'
				print >>fp, '\t\t\t\tbreak;'
				print >>fp, '\t\t\t}'
				cmdNum += 1
			print >>fp, '\t\t\tdefault:'
			print >>fp, '\t\t\t\tthrow new UnknownCommandException();'
			print >>fp, '\t\t}'
			print >>fp, '\t}'
			print >>fp, '}'
			print >>fp, 'public class Remote%s : %s%s {' % (title(name), 'RemoteObject, ' if not isObject else 'IRemoteObject, ', title(name))
			if isObject:
				print >>fp, '\tprotected readonly IConnection Connection;'
				print >>fp, '\tpublic ulong ObjectId { get; }'
				print >>fp, '\tpublic RemoteObject(IConnection connection, ulong id) {'
				print >>fp, '\t\tConnection = connection;'
				print >>fp, '\t\tObjectId = id;'
				print >>fp, '\t}'
			else:
				print >>fp, '\tpublic Remote%s(IConnection connection, ulong id) : base(connection, id) {}' % title(name)
			cmdNum = 0
			if not isObject:
				cmdNum = 2
			for method in iface['methods']:
				print >>fp, '\tpublic async %s %s(%s) {' % (typed(method['ret'], isRet=True), title(method['name']), ', '.join('%s %s' % (typed(arg['ty']), named(arg['name'])) for arg in method['args']))
				if len(method['args']) or method['ret'] is not None:
					print >>fp, '\t\tvar offset = 0;'
				if len(method['args']) == 0:
					print >>fp, '\t\t%sawait Connection.Call(ObjectId, %i, Memory<byte>.Empty);' % ('var buf = ' if method['ret'] is not None else '', cmdNum)
				else:
					print >>fp, '\t\tMemory<byte> buf = new byte[%s];' % ' + '.join(genMsgSize(arg['ty'], named(arg['name'])) for arg in method['args'])
					for arg in method['args']:
						genMsgSerialize(arg['ty'], named(arg['name']))
					print >>fp, '\t\t%sawait Connection.Call(ObjectId, %i, buf);' % ('buf = ' if method['ret'] is not None else '', cmdNum)
					print >>fp, '\t\toffset = 0;'
				if method['ret'] is not None:
					genMsgDeserialize(method['ret'], 'ret')
					print >>fp, '\t\treturn ret;'
				print >>fp, '\t}'
				cmdNum += 1
			print >>fp, '}'
		for name, struct in ns['structs'].items():
			print >>fp
			print >>fp, 'public struct %s {' % title(name)
			for type, fname in struct:
				print >>fp, '\tpublic %s %s;' % (typed(type), title(fname))
			print >>fp
			if len(struct) == 0:
				print >>fp, '\tpublic int SerializedSize => 0;'
			else:
				print >>fp, '\tpublic int SerializedSize => %s;' % ' + '.join(genSize(type, title(fname)) for type, fname in struct)
			print >>fp, '\tpublic void Serialize(IConnection connection, Span<byte> buf, ref int offset) {'
			for type, fname in struct:
				genSerialize(type, title(fname))
			print >>fp, '\t}'
			print >>fp, '\tpublic static %s Deserialize(IConnection connection, Span<byte> buf, ref int offset) {' % title(name)
			print >>fp, '\t\tvar obj = new %s();' % title(name)
			for type, fname in struct:
				genDeserialize(type, 'obj.' + title(fname))
			print >>fp, '\t\treturn obj;'
			print >>fp, '\t}'
			print >>fp, '}'
		for name, (isFlags, backing, enum) in ns['enums'].items():
			print >>fp
			if True or isFlags: # XXX: DISABLE WHEN WE GET FLAGS BACK!
				print >>fp, '[Flags]'
			print >>fp, 'public enum %s : %s {' % (title(name), typed(backing))
			for ename, value in enum:
				print >>fp, '\t%s = %s,' % (title(ename), value)
			print >>fp, '}'
