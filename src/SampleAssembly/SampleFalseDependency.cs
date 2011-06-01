/*
 * Created by SharpDevelop.
 * User: jcastro
 * Date: 2011-05-25
 * Time: 3:57 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;

namespace SampleAssembly
{
	/// <summary>
	/// Description of Class1.
	/// </summary>
	public class SampleFalseDependency:IFalseDependency
	{
		public SampleFalseDependency()
		{
		}
		public string FalseDependency{get; set;}
	}
}
