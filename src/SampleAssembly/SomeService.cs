/*
 * Created by SharpDevelop.
 * User: jcastro
 * Date: 2011-05-26
 * Time: 11:58 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;

namespace SampleAssembly
{
	public interface ISomeService
	{
		int Value
		{
			get; set;
		}
	}
	/// <summary>
	/// Description of Class1.
	/// </summary>
	public class SomeService:ISomeService
	{
		public SomeService()
		{
		}
		
		public int Value{get; set;}
	}
}
