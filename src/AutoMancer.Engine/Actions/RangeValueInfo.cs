// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
namespace AutoMancer.Engine.Actions;

// A range control's (slider, progress bar, scrollbar) current value and its declared bounds.
public readonly record struct RangeValueInfo(double Value, double Minimum, double Maximum);
