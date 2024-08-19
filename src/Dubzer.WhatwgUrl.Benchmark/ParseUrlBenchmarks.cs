		Top100
	[Params(TestSet.UrlTestData, TestSet.UrlTestDataValidOnly, TestSet.Top100)]
		else if (DataSet == TestSet.Top100)
			_data = File.ReadLines("Resources/top100.txt")
