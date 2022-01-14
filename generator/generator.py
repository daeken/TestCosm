from yaml import load
try:
	from yaml import CLoader as Loader
except ImportError:
	from yaml import Loader

spec = load(file('spec.yaml'), Loader=Loader)

import pprint
pprint.pprint(spec)

namespaces = {}

namespace = None

def genName(name):
	if '.' in name:
		return name
	assert namespace is not None

	nsl = namespace.split('.')
	name = '.'.join(nsl[:-3] + [name] + nsl[-3:])
	return name

def splitNamespace(name):
	nl = name.split('.')
	namespace = '.'.join(nl[:-4])
	name = nl[-4]
	return namespace, name

def getNs(namespace):
	if namespace not in namespaces:
		namespaces[namespace] = dict(
			structs={}, 
			interfaces={}, 
			enums={}
		)
	return namespaces[namespace]

def title(x):
	return x[0].upper() + x[1:]

def parseType(type):
	if type.startswith('array['):
		return ['array', parseType(type[6:-1])]
	elif type.startswith('callback['):
		type = type[9:-1] + ','
		elems = []
		bracket = 0
		last = 0
		for i, c in enumerate(type):
			if c == '[':
				bracket += 1
			elif c == ']':
				bracket -= 1
			elif bracket == 0 and c == ',':
				elems.append(type[last:i])
				last = i + 1
		return ['callback'] + map(parseType, elems)
	else:
		return [type]

def parseInterface(elem): # TODO: Handle inheritance
	if 'name' in elem:
		name = genName(elem['name'])
	else:
		name = namespace
	assert name is not None

	def proc(elem):
		if isinstance(elem, dict):
			name = elem.keys()[0]
			oparams = elem[name]
			params = []
			for elem in oparams:
				if isinstance(elem, dict):
					type = elem.keys()[0]
					pname = elem[type]
					params.append((type, pname))
				else:
					params.append(elem)
		else:
			name = elem
			params = []
		if len(params) == 0:
			params = ['void']
		return name, params

	events = map(proc, elem['events']) if 'events' in elem else []
	methods = map(proc, elem['methods']) if 'methods' in elem else []

	emethods = []
	for ename, params in events:
		if len(params) == 1:
			cparams = 'callback[%s]' % params[0], 'callback'
		else:
			cparams = 'callback[%s,%s]' % (params[0], ','.join(type for type, _ in params[1:])), 'callback'
		emethods.append(('Subscribe' + title(ename), ['void', cparams]))
		emethods.append(('Unsubscribe' + title(ename), ['void', cparams]))

	methods = emethods + methods
	methods = [(ename, [parseType(params[0])] + [(parseType(type), pname) for type, pname in params[1:]]) for ename, params in methods]

	ns, name = splitNamespace(name)
	if 'name' not in elem:
		ns += '.' + name
	ns = getNs(ns)
	ns['interfaces'][name] = {'methods' : methods}

def parseExtension(elem):
	global namespace
	assert len(elem) >= 1 and 'name' in elem[0]
	namespace = elem[0]['name']
	nsl = namespace.split('.')
	assert len(nsl) > 3 and nsl[-3].startswith('v')
	parse(elem[1:])

def parseStruct(elem):
	assert len(elem) >= 1 and 'name' in elem[0]
	name = genName(elem[0]['name'])
	ns, name = splitNamespace(name)
	ns = getNs(ns)
	def proc(elem):
		type = elem.keys()[0]
		return parseType(type), elem[type]
	ns['structs'][name] = map(proc, elem[1:])

def parseFlags(elem):
	assert len(elem) >= 2 and 'name' in elem[0] and 'type' in elem[1]
	name = genName(elem[0]['name'])
	type = elem[1]['type'] # TODO: Support enum types
	ns, name = splitNamespace(name)
	ns = getNs(ns)
	bit = [0]
	def proc(elem):
		if isinstance(elem, dict):
			name = elem.keys()[0]
			return (name, elem[name])
		value = 1 << bit[0]
		bit[0] += 1
		return (elem, value)
	ns['enums'][name] = (False, map(proc, elem[2:]))

def parseEnum(elem):
	pass

def parse(data):
	for elem in data:
		if 'interface' in elem:
			parseInterface(elem['interface'])
		elif 'extension' in elem:
			parseExtension(elem['extension'])
		elif 'struct' in elem:
			parseStruct(elem['struct'])
		elif 'flags' in elem:
			parseFlags(elem['flags'])
		elif 'enum' in elem:
			parseEnum(elem['enum'])
		else:
			print 'Unhandled:', `elem`

parse(spec)

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
		if type == ['void']:
			return 'Task'
		else:
			return 'Task<%s>' % typed(type)
	if type[0] == 'array':
		return '%s[]' % typed(type[1])
	elif type[0] == 'callback':
		ret = typed(type[1], True)
		params = type[2:]
		if len(params):
			return 'Func<%s, %s>' % (', '.join(map(typed, params)), ret)
		else:
			return 'Func<%s>' % ret
	elif type[0] in btypes:
		return btypes[type[0]]
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
			ws = '\t\t' + '\t' * depth
			if type[0] == 'array':
				print >>fp, ws + '%s = new %s[(int) NetExtensions.DeserializeVu64(buf, ref offset)];' % (name, typed(type[1]))
				print >>fp, ws + 'for(var i%i = 0; i%i < %s.Length; ++i%i) {' % (depth, depth, name, depth)
				genDeserialize(type[1], '%s[i%i]' % (name, depth), depth + 1)
				print >>fp, ws + '}'
			elif type[0] in btypes and type[0] != 'uuid':
				print >>fp, ws + '%s = NetExtensions.Deserialize%s(buf, ref offset);' % (name, title(type[0]))
			elif type[0] in ns['enums']:
				print >>fp, ws + '%s = (%s) NetExtensions.DeserializeVu64(buf, ref offset);' % (name, title(type[0])) # TODO: Support enum types
			elif type[0] == 'object' or type[0] in ns['interfaces']:
				print >>fp, ws + '%s = connection.GetObject<%s>(NetExtensions.DeserializeVu64(buf, ref offset), _id => new Remote%s(connection, _id));' % (name, title(type[0]), title(type[0]))
			elif type[0] == 'uuid':
				print >>fp, ws + '%s = Uuid.Deserialize(buf, ref offset);' % name
			else:
				print >>fp, ws + '%s = %s.Deserialize(connection, buf, ref offset);' % (name, title(type[0]))
		def genMsgDeserialize(type, name, depth = 0, depthOffset = 0):
			ws = '\t\t' + '\t' * (depth + depthOffset)
			var = 'var ' if depth == 0 else ''
			if type[0] == 'array':
				print >>fp, ws + '%s%s = new %s[(int) NetExtensions.DeserializeVu64(buf.Span, ref offset)];' % (var, name, typed(type[1]))
				print >>fp, ws + 'for(var i%i = 0; i%i < %s.Length; ++i%i) {' % (depth, depth, name, depth)
				genMsgDeserialize(type[1], '%s[i%i]' % (name, depth), depth + 1, depthOffset)
				print >>fp, ws + '}'
			elif type[0] in btypes and type[0] != 'uuid':
				print >>fp, ws + '%s%s = NetExtensions.Deserialize%s(buf.Span, ref offset);' % (var, name, title(type[0]))
			elif type[0] in ns['enums']:
				print >>fp, ws + '%s%s = (%s) NetExtensions.DeserializeVu64(buf.Span, ref offset);' % (var, name, title(type[0])) # TODO: Support enum types
			elif type[0] == 'object' or type[0] in ns['interfaces']:
				print >>fp, ws + '%s%s = Connection.GetObject<%s>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id => new Remote%s(Connection, _id));' % (var, name, title(type[0]), title(type[0]))
			elif type[0] == 'callback':
				print >>fp, ws + '%s%s = Connection.GetCallback<%s>(NetExtensions.DeserializeVu64(buf.Span, ref offset), _id =>' % (var, name, typed(type))
				print >>fp, ws + '\tasync (%s) => {' % ', '.join('_p%i' % i for i, _ in enumerate(type[2:]))
				params = type[1:]
				if len(params) > 1 or params[0] != ['void']:
					print >>fp, ws + '\t\tvar offset = 0;'
				if len(params) == 1:
					print >>fp, ws + '\t\t%sawait Connection.Call(_id, 2, Memory<byte>.Empty);' % ('var buf = ' if params[0] != ['void'] else '')
				else:
					print >>fp, ws + '\t\tMemory<byte> buf = new byte[%s];' % ' + '.join(genMsgSize(type, '_p%i' % i) for i, type in enumerate(params[1:]))
					for i, type in enumerate(params[1:]):
						genMsgSerialize(type, '_p%i' % i, 4)
					print >>fp, ws + '\t\t%sawait Connection.Call(_id, 2, buf);' % ('buf = ' if params[0] != ['void'] else '')
					print >>fp, ws + '\t\toffset = 0;'
				if params[0] != ['void']:
					genMsgDeserialize(params[0], 'ret', depth + 1, depthOffset)
					print >>fp, ws + '\t\treturn ret;'
				print >>fp, ws + '\t}'
				print >>fp, ws + ');'
			elif type[0] == 'uuid':
				print >>fp, ws + '%s%s = Uuid.Deserialize(buf.Span, ref offset);' % (var, name)
			else:
				print >>fp, ws + '%s%s = %s.Deserialize(Connection, buf.Span, ref offset);' % (var, name, title(type[0]))
		def genSerialize(type, value, depth = 0):
			ws = '\t\t' + '\t' * depth
			if type[0] == 'array':
				print >>fp, ws + 'NetExtensions.SerializeVu64((ulong) %s.Length, buf, ref offset);' % value
				print >>fp, ws + 'foreach(var _%i in %s) {' % (depth, value)
				genSerialize(type[1], '_%i' % depth, depth+1)
				print >>fp, ws + '}'
			elif type[0] in btypes and type[0] != 'uuid':
				print >>fp, ws + 'NetExtensions.Serialize%s(%s, buf, ref offset);' % (title(type[0]), value)
			elif type[0] in ns['enums']:
				genSerialize(['vu64'], '(ulong) ' + value, depth) # TODO: Support enum types
			elif type[0] == 'object' or type[0] in ns['interfaces']:
				genSerialize(['vu64'], '%s.ObjectId' % value, depth)
			elif type[0] == 'uuid':
				print >>fp, ws + '%s.Serialize(buf, ref offset);' % value
			elif type[0] == 'callback':
				print >>fp, ws + 'NetExtensions.SerializeVu64(connection.GetCallbackId(%s), buf, ref offset);' % value
			else:
				print >>fp, ws + '%s.Serialize(connection, buf, ref offset);' % value
		def genMsgSerialize(type, value, depth = 0):
			ws = '\t\t' + '\t' * depth
			if type[0] == 'array':
				print >>fp, ws + 'NetExtensions.SerializeVu64((ulong) %s.Length, buf.Span, ref offset);' % value
				print >>fp, ws + 'foreach(var _%i in %s) {' % (depth, value)
				genMsgSerialize(type[1], '_%i' % depth, depth+1)
				print >>fp, ws + '}'
			elif type[0] in btypes and type[0] != 'uuid':
				print >>fp, ws + 'NetExtensions.Serialize%s(%s, buf.Span, ref offset);' % (title(type[0]), value)
			elif type[0] in ns['enums']:
				genMsgSerialize(['vu64'], '(ulong) ' + value, depth) # TODO: Support enum types
			elif type[0] == 'object' or type[0] in ns['interfaces']:
				genMsgSerialize(['vu64'], '%s.ObjectId' % value, depth)
			elif type[0] == 'uuid':
				print >>fp, ws + '%s.Serialize(buf.Span, ref offset);' % value
			elif type[0] == 'callback':
				print >>fp, ws + 'NetExtensions.SerializeVu64(Connection.GetCallbackId(%s, () =>' % value
				print >>fp, ws + '\tasync (sequence, buf) => {'
				params = type[1:]
				if len(params) > 1 or params[0] != ['void']:
					print >>fp, ws + '\t\tvar offset = 0;'
				for i, type in enumerate(params[1:]):
					genMsgDeserialize(type, '_p%i' % i, depthOffset=2)
				if params[0] == ['void']:
					ret = ''
				else:
					ret = 'var __ret = '
				print >>fp, ws + '\t\t%sawait %s(%s);' % (ret, value, ', '.join('_p%i' % i for i, _ in enumerate(params[1:])))
				print >>fp, ws + '\t\tif(sequence != 0) {'
				if params[0] == ['void']:
					print >>fp, ws + '\t\t\tawait Connection.Respond(sequence, Memory<byte>.Empty);'
				else:
					print >>fp, ws + '\t\t\tbuf = new byte[%s];' % genMsgSize(params[0], '__ret')
					print >>fp, ws + '\t\t\toffset = 0;'
					genMsgSerialize(params[0], '__ret', depth=3)
					print >>fp, ws + '\t\t\tawait Connection.Respond(sequence, buf);'
				print >>fp, ws + '\t\t}'
				print >>fp, ws + '\t}'
				print >>fp, ws + '), buf.Span, ref offset);'
			else:
				print >>fp, ws + '%s.Serialize(Connection, buf.Span, ref offset);' % value
		def genSize(type, value, depth = 0):
			if type[0] == 'array':
				return 'NetExtensions.SizeVu64((ulong) %s.Length) + %s.Select(_%i => %s).Sum()' % (value, value, depth, genSize(type[1], '_%i' % depth, depth + 1))
			elif type[0] == 'uuid':
				return '16'
			elif type[0] in btypes:
				return 'NetExtensions.Size%s(%s)' % (title(type[0]), value)
			elif type[0] in ns['enums']:
				return 'NetExtensions.SizeVu64((ulong) %s)' % value
			elif type[0] == 'object' or type[0] in ns['interfaces']:
				return 'NetExtensions.SizeVu64(%s.ObjectId)' % value
			elif type[0] == 'callback':
				return 'NetExtensions.SizeVu64(connection.GetCallbackId(%s))' % value
			else:
				return '%s.SerializedSize' % value
		def genMsgSize(type, value, depth = 0):
			if type[0] == 'callback':
				return 'NetExtensions.SizeVu64(Connection.GetCallbackId(%s))' % value
			return genSize(type, value, depth)

		for name, iface in ns['interfaces'].items():
			print >>fp
			print >>fp, 'public interface %s%s {' % (title(name), ' : Object' if nsn != 'hypercosm' or name != 'object' else '')
			if nsn == 'hypercosm' and name == 'object':
				print >>fp, '\tulong ObjectId { get; }'
			for mname, params in iface['methods']:
				print >>fp, '\t%s %s(%s);' % (typed(params[0], isRet=True), title(mname), ', '.join('%s %s' % (typed(type), pname) for type, pname in params[1:]))
			print >>fp, '}'
			print >>fp, 'public abstract class Base%s : %s%s {' % (title(name), 'BaseObject, ' if nsn != 'hypercosm' or name != 'object' else 'ILocalObject, ', title(name))
			if nsn == 'hypercosm' and name == 'object':
				print >>fp, '\tprotected readonly IConnection Connection;'
				print >>fp, '\tpublic ulong ObjectId { get; }'
				print >>fp, '\tprotected BaseObject(IConnection connection) {'
				print >>fp, '\t\tConnection = connection;'
				print >>fp, '\t\tObjectId = Connection.RegisterLocalObject(this);'
				print >>fp, '\t}'
			else:
				print >>fp, '\tprotected Base%s(IConnection connection) : base(connection) {}' % title(name)
			for mname, params in iface['methods']:
				print >>fp, '\tpublic abstract %s %s(%s);' % (typed(params[0], isRet=True), title(mname), ', '.join('%s %s' % (typed(type), pname) for type, pname in params[1:]))
			print >>fp
			print >>fp, '\tpublic %s async Task HandleMessage(ulong sequence, int commandNumber, Memory<byte> buf, int offset) {' % ('override' if nsn != 'hypercosm' or name != 'object' else 'virtual')
			print >>fp, '\t\tswitch(commandNumber) {'
			cmdNum = 0
			if nsn != 'hypercosm' or name != 'object':
				cmdNum = 2
				print >>fp, '\t\t\tcase 0 or 1: await base.HandleMessage(sequence, commandNumber, buf, offset); break;'
			for mname, params in iface['methods']:
				print >>fp, '\t\t\tcase %i: {' % cmdNum
				for type, pname in params[1:]:
					genMsgDeserialize(type, title(pname), depthOffset=2)
				if params[0] == ['void']:
					ret = ''
				else:
					ret = 'var __ret = '
				print >>fp, '\t\t\t\t%sawait %s(%s);' % (ret, title(mname), ', '.join(title(pname) for _, pname in params[1:]))
				print >>fp, '\t\t\t\tif(sequence != 0) {'
				if params[0] == ['void']:
					print >>fp, '\t\t\t\t\tawait Connection.Respond(sequence, Memory<byte>.Empty);'
				else:
					print >>fp, '\t\t\t\t\tbuf = new byte[%s];' % genMsgSize(params[0], '__ret')
					print >>fp, '\t\t\t\t\toffset = 0;'
					genMsgSerialize(params[0], '__ret', depth=3)
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
			print >>fp, 'public class Remote%s : %s%s {' % (title(name), 'RemoteObject, ' if nsn != 'hypercosm' or name != 'object' else 'IRemoteObject, ', title(name))
			if nsn == 'hypercosm' and name == 'object':
				print >>fp, '\tprotected readonly IConnection Connection;'
				print >>fp, '\tpublic ulong ObjectId { get; }'
				print >>fp, '\tpublic RemoteObject(IConnection connection, ulong id) {'
				print >>fp, '\t\tConnection = connection;'
				print >>fp, '\t\tObjectId = id;'
				print >>fp, '\t}'
			else:
				print >>fp, '\tpublic Remote%s(IConnection connection, ulong id) : base(connection, id) {}' % title(name)
			cmdNum = 0
			if nsn != 'hypercosm' or name != 'object':
				cmdNum = 2
			for mname, params in iface['methods']:
				print >>fp, '\tpublic async %s %s(%s) {' % (typed(params[0], isRet=True), title(mname), ', '.join('%s %s' % (typed(type), pname) for type, pname in params[1:]))
				if len(params) > 1 or params[0] != ['void']:
					print >>fp, '\t\tvar offset = 0;'
				if len(params) == 1:
					print >>fp, '\t\t%sawait Connection.Call(ObjectId, %i, Memory<byte>.Empty);' % ('var buf = ' if params[0] != ['void'] else '', cmdNum)
				else:
					print >>fp, '\t\tMemory<byte> buf = new byte[%s];' % ' + '.join(genMsgSize(type, pname) for type, pname in params[1:])
					for type, pname in params[1:]:
						genMsgSerialize(type, pname)
					print >>fp, '\t\t%sawait Connection.Call(ObjectId, %i, buf);' % ('buf = ' if params[0] != ['void'] else '', cmdNum)
					print >>fp, '\t\toffset = 0;'
				if params[0] != ['void']:
					genMsgDeserialize(params[0], 'ret')
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
		for name, (isFlags, enum) in ns['enums'].items():
			print >>fp
			if isFlags:
				print >>fp, '[Flags]'
			print >>fp, 'public enum %s : ulong {' % title(name) # TODO: Support enum types
			for ename, value in enum:
				print >>fp, '\t%s = %s,' % (title(ename), value)
			print >>fp, '}'
