﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hiro.UnitTests.SampleDomain
{
    public class Vehicle
    {
        public Vehicle()
        {
        }
        public Vehicle(IPerson driver)
        {
            Driver = driver;
        }

        public IPerson Driver { get; set; }
    }
}