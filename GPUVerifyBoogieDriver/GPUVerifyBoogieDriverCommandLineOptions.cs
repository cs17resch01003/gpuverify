﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Boogie;

namespace GPUVerifyBoogieDriver {
  public class GPUVerifyBoogieDriverCommandLineOptions : CommandLineOptions {

    public GPUVerifyBoogieDriverCommandLineOptions() :
      base("GPUVerify", "GPUVerify kernel analyser") {
    }
  }
}