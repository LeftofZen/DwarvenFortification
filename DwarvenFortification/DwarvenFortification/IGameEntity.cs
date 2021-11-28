using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DwarvenFortification
{
	// an interface to represent any kind of entity in the game world itself
	public interface IGameEntity
	{
		string Name { get; }
	}
}
