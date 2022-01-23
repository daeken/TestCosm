using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Compatibility;
using MoonSharp.Interpreter.Interop;
using MoonSharp.Interpreter.Interop.BasicDescriptors;

namespace RuntimeLib; 

/// <summary>
///     Class providing easier marshalling of CLR functions
/// </summary>
public class DynamicMethodMemberDescriptor : FunctionMemberDescriptorBase, IOptimizableDescriptor, IWireableDescriptor {
	readonly bool m_IsAction;
	readonly Delegate m_Delegate;
	Action<object, object[]> m_OptimizedAction;


	Func<object, object[], object> m_OptimizedFunc;

	/// <summary>
	///     Initializes a new instance of the <see cref="MethodMemberDescriptor" /> class.
	/// </summary>
	/// <param name="methodInfo">The MethodBase (MethodInfo or ConstructorInfo) got through reflection.</param>
	/// <param name="accessMode">The interop access mode.</param>
	/// <exception cref="System.ArgumentException">Invalid accessMode</exception>
	public DynamicMethodMemberDescriptor(Delegate @delegate, MethodInfo methodInfo, InteropAccessMode accessMode = InteropAccessMode.Default) {
		CheckMethodIsCompatible(methodInfo, true);

		m_Delegate = @delegate;

		MethodInfo = methodInfo;
		m_IsAction = MethodInfo.ReturnType == typeof(void);

		var reflectionParams = methodInfo.GetParameters();

		var parameters = reflectionParams.Skip(1).Select(pi => new ParameterDescriptor(pi)).ToArray();

		var isExtensionMethod = methodInfo.IsStatic && parameters.Length > 0 && methodInfo.GetCustomAttributes(typeof(ExtensionAttribute), false).Any();

		Initialize(methodInfo.Name, false, parameters, isExtensionMethod);

		// adjust access mode
		if(Script.GlobalOptions.Platform.IsRunningOnAOT())
			accessMode = InteropAccessMode.Reflection;

		if(accessMode == InteropAccessMode.Default)
			accessMode = UserData.DefaultAccessMode;

		if(accessMode == InteropAccessMode.HideMembers)
			throw new ArgumentException("Invalid accessMode");

		if(parameters.Any(p => p.Type.IsByRef))
			accessMode = InteropAccessMode.Reflection;

		AccessMode = accessMode;

		if(AccessMode == InteropAccessMode.Preoptimized)
			((IOptimizableDescriptor) this).Optimize();
	}

	/// <summary>
	///     Gets the method information (can be a MethodInfo or ConstructorInfo)
	/// </summary>
	public MethodInfo MethodInfo { get; }

	/// <summary>
	///     Gets the access mode used for interop
	/// </summary>
	public InteropAccessMode AccessMode { get; }

	/// <summary>
	///     Gets a value indicating whether the described method is a constructor
	/// </summary>
	public bool IsConstructor { get; }

	/// <summary>
	///     Called by standard descriptors when background optimization or preoptimization needs to be performed.
	/// </summary>
	/// <exception cref="InternalErrorException">Out/Ref params cannot be precompiled.</exception>
	void IOptimizableDescriptor.Optimize() {
		var parameters = Parameters;

		if(AccessMode == InteropAccessMode.Reflection)
			return;

		var ep = Expression.Parameter(typeof(object[]), "pars");
		var objinst = Expression.Parameter(typeof(object), "instance");
		var inst = objinst;

		var args = new Expression[parameters.Length];

		for(var i = 0; i < parameters.Length; i++)
			if(parameters[i].OriginalType.IsByRef)
				throw new Exception("Out/Ref params cannot be precompiled.");
			else {
				var x = Expression.ArrayIndex(ep, Expression.Constant(i));
				args[i] = Expression.Convert(x, parameters[i].OriginalType);
			}

		Expression fn = Expression.Call(Expression.Constant(m_Delegate), m_Delegate.GetType().GetMethod("DynamicInvoke") ?? throw new InvalidOperationException(), 
			Expression.NewArrayInit(typeof(object), args.Select(x => Expression.Convert(x, typeof(object)))));


		if(m_IsAction) {
			var lambda = Expression.Lambda<Action<object, object[]>>(fn, objinst, ep);
			Interlocked.Exchange(ref m_OptimizedAction, lambda.Compile());
		} else {
			var fnc = Expression.Convert(fn, typeof(object));
			var lambda = Expression.Lambda<Func<object, object[], object>>(fnc, objinst, ep);
			Interlocked.Exchange(ref m_OptimizedFunc, lambda.Compile());
		}
	}


	/// <summary>
	///     Prepares the descriptor for hard-wiring.
	///     The descriptor fills the passed table with all the needed data for hardwire generators to generate the appropriate
	///     code.
	/// </summary>
	/// <param name="t">The table to be filled</param>
	public void PrepareForWiring(Table t) {
		t.Set("class", DynValue.NewString(GetType().FullName));
		t.Set("name", DynValue.NewString(Name));
		t.Set("ctor", DynValue.NewBoolean(IsConstructor));
		t.Set("special", DynValue.NewBoolean(MethodInfo.IsSpecialName));
		t.Set("visibility", DynValue.NewString(MethodInfo.GetClrVisibility()));

		t.Set("ret", DynValue.NewString(MethodInfo.ReturnType.FullName));

		t.Set("decltype", DynValue.NewString("wasm"));
		t.Set("static", DynValue.NewBoolean(IsStatic));
		t.Set("extension", DynValue.NewBoolean(ExtensionMethodType != null));

		var pars = DynValue.NewPrimeTable();

		t.Set("params", pars);

		var i = 0;

		foreach(var p in Parameters) {
			var pt = DynValue.NewPrimeTable();
			pars.Table.Set(++i, pt);
			p.PrepareForWiring(pt.Table);
		}
	}

	/// <summary>
	///     Tries to create a new MethodMemberDescriptor, returning
	///     <c>null</c> in case the method is not
	///     visible to script code.
	/// </summary>
	/// <param name="methodBase">The MethodBase.</param>
	/// <param name="accessMode">The <see cref="InteropAccessMode" /></param>
	/// <param name="forceVisibility">if set to <c>true</c> forces visibility.</param>
	/// <returns>
	///     A new MethodMemberDescriptor or null.
	/// </returns>
	public static MethodMemberDescriptor TryCreateIfVisible(MethodBase methodBase, InteropAccessMode accessMode, bool forceVisibility = false) {
		if(!CheckMethodIsCompatible(methodBase, false))
			return null;

		if(forceVisibility || (methodBase.GetVisibilityFromAttributes() ?? methodBase.IsPublic))
			return new MethodMemberDescriptor(methodBase, accessMode);

		return null;
	}

	/// <summary>
	///     Checks if the method is compatible with a standard descriptor
	/// </summary>
	/// <param name="methodBase">The MethodBase.</param>
	/// <param name="throwException">
	///     if set to <c>true</c> an exception with the proper error message is thrown if not
	///     compatible.
	/// </param>
	/// <returns></returns>
	/// <exception cref="System.ArgumentException">
	///     Thrown if throwException is <c>true</c> and one of this applies:
	///     The method contains unresolved generic parameters, or has an unresolved generic return type
	///     or
	///     The method contains pointer parameters, or has a pointer return type
	/// </exception>
	public static bool CheckMethodIsCompatible(MethodBase methodBase, bool throwException) {
		if(methodBase.ContainsGenericParameters) {
			if(throwException) throw new ArgumentException("Method cannot contain unresolved generic parameters");
			return false;
		}

		if(methodBase.GetParameters().Any(p => p.ParameterType.IsPointer)) {
			if(throwException) throw new ArgumentException("Method cannot contain pointer parameters");
			return false;
		}

		var mi = methodBase as MethodInfo;

		if(mi != null) {
			if(mi.ReturnType.IsPointer) {
				if(throwException) throw new ArgumentException("Method cannot have a pointer return type");
				return false;
			}

			if(Framework.Do.IsGenericTypeDefinition(mi.ReturnType)) {
				if(throwException) throw new ArgumentException("Method cannot have an unresolved generic return type");
				return false;
			}
		}

		return true;
	}

	/// <summary>
	///     The internal callback which actually executes the method
	/// </summary>
	/// <param name="script">The script.</param>
	/// <param name="obj">The object.</param>
	/// <param name="context">The context.</param>
	/// <param name="args">The arguments.</param>
	/// <returns></returns>
	public override DynValue Execute(Script script, object obj, ScriptExecutionContext context, CallbackArguments args) {
		this.CheckAccess(MemberDescriptorAccess.CanExecute, obj);

		if(AccessMode == InteropAccessMode.LazyOptimized &&
		   m_OptimizedFunc == null && m_OptimizedAction == null)
			((IOptimizableDescriptor) this).Optimize();

		List<int> outParams = null;
		var pars = base.BuildArgumentList(script, obj, context, args, out outParams);
		object retv = null;

		if(m_OptimizedFunc != null)
			retv = m_OptimizedFunc(obj, pars);
		else if(m_OptimizedAction != null) {
			m_OptimizedAction(obj, pars);
			retv = DynValue.Void;
		} else if(m_IsAction) {
			MethodInfo.Invoke(obj, pars);
			retv = DynValue.Void;
		} else {
			retv = MethodInfo.Invoke(obj, pars);
		}

		return BuildReturnValue(script, outParams, pars, retv);
	}
}