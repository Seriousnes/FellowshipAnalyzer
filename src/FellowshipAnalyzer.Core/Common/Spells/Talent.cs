using System;
using System.Collections.Generic;
using System.Text;

namespace FellowshipAnalyzer.Core.Common.Spells;

public record Talent(
    int Id, 
    string Name = "", 
    string Icon = ""
);
