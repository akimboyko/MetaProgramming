# -*- coding: utf8 -*-

import math
import clr
clr.AddReference("LinqPadAssemblyName") # linq snippet assembly name

from Model import *

class BusinessRule:
	def calculate(self, model):
		result = model.InputA + model.InputB * model.Factor
		delta = math.fabs(result - model.InputA)
		description = 'Some description'

		resultModel = ReportModel()
		resultModel.Σ = result
		resultModel.Δ = delta
		resultModel.λ = description

		return resultModel