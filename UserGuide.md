# Introduction

This data provider allow you to run Javascript code to construct a tabular result set.

In Wyn, this data provider is treated as a Native data provider, which means that you can only use it in the following scenarios:
* Native dataset designer
* CustomSQLTable in direct/cache dataset designer

# Connection string pattern

Sample:
```
	Engine = Jint;
	LimitMemory = 100;
	TimeoutInterval = 30;
	MaxStatements = 2000;
```

* Engine: Must-have. Currently this provider supports only Jint script engine.
* LimitMemory: Optional. Limits the memory allocation to given amount in MB. Default is 100MB.
* TimeoutInterval: Optional. The timeout interval in seconds. Default is 30 seconds.
* MaxStatements: Optional. The maximum number of script statements that can be executed. Default is 2000.

# Command text pattern

The command text is a Javascript code snippet. 
The code snippet should be written with native Javascript, which means functions like fetch() is not accessible. This provider injects some objects to help you construct the result set.

## variable: resultset

Sample code setting resultset schema:
```
	resultset.schema({
		column1: 'string',
		column2: 'integer'
	});
```
Available data types are `string`, `integer`, `double`, `decimal`, `boolean` and `datetime`. Invalid data types will be treated as `string`.

Sample code adding a single row to resultset:
```
	resultset.row(['value1', 123]);
```
or
```
	resultset.row({column1:'value1', column2:123});
```
If the parameter is an array, the order of the values should match the order of the columns in the schema. 
The array length can be less or greater than the column count, in which case the mismatched columns will be filled with null values.

If the parameter is an object, the keys should match the column names in the schema. The object can contain more or less keys than the column count, in which case the mismatched columns will be filled with null values.

## variable: helper

As the most common use case is to fetch data via RESTful API, this provider injects a helper object to help you.

Sample code fetching data from RESTful API:
```
	var response = helper.fetch('https://jsonplaceholder.typicode.com/posts');
```
or
```
	var response = helper.fetch('https://jsonplaceholder.typicode.com/posts', {
		method: 'GET',
		headers: {
			'Content-Type': 'application/json'
		}
	});
```

This fetch() function is different from the popular browser fetch() function. It does not return a promise. Instead, it returns a response object.
You can access the following properties of the response object:
* status: Integer. The HTTP status code. 
* statusText: String. The HTTP status text.
* headers: Object. The response headers.
* body: String. The literal raw response body.

## Complete sample command text

```
	resultset.schema({
        userId: 'integer',
        id: 'integer',
        title: 'string',
        body: 'string'
    });

    var response = helper.fetch('https://jsonplaceholder.typicode.com/posts');
    JSON.parse(response.body).forEach(r => resultset.row(r));
```

# Data type conversion

The types defined in the schema are used to convert the values in the row.
If the value cannot be converted to the specified type, an exception will be thrown.

# Disclaimer

* The source code of this data provider is provided as-is. You can modify it to fit your needs. Also, you can redistribute it without any restrictions.
* This data provider is a demo provider published by Wyn team for the purpose of demonstrating how to create a custom data provider. It is not guaranteed to work in all scenarios. If you have any questions, please post them in the Wyn forum.
