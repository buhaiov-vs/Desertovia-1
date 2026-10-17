#nullable disable
﻿namespace Nez.AI.UtilityAI
{
	public interface IAction<T>
	{
		void Execute(T context);
	}
}