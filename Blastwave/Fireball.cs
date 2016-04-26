using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blastwave
{
    class Fireball
    {
        float yield;        //in J
        float maxRadius;    //in m

        void CalcMaxRad()
        {
            maxRadius = 90 * (float)Math.Pow(yield, 2f / 5f);       //note, this needs to be converted from ft to m and yield needs to be converted to kT TNT
        }
    }
}
