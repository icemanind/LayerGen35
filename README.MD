# Introduction
A while back (like 7 years ago), I released a little program called "LayerGen MMX". I had started LayerGen back in 2002 around the time the .NET framework first got released. After LayerGen MMX, I wanted to completely rewrite the program from scratch. I did that and 7 years later, I am proud to present LayerGen 3.5.

If you are unfamiliar with LayerGen, it is an ORM tool that will automatically generate Data Layers and Business Layers based off a database schema. It will generate code in either C# or VB.NET. It currently works with Microsoft SQL Server, versions 2000 and up, Microsoft Access, SQLite or MySql. In addition, the code it generates should work with .NET 2.0 up to the current .NET version. In addition, you can also configure LayerGen dynamically to work with either Stored Procedures or straight SQL text (note: Microsoft Access and SQLite do not support stored procedures).

After using LayerGen for a while, you will soon come to realize that LayerGen's biggest selling point and feature is its ease of use and intuitiveness. Compared with similar products, LayerGen makes accessing databases a breeze.

# Tutorial Walkthrough
## Introduction & Prerequisites
I think the best way to learn how to use LayerGen is to dive into a tutorial. LayerGen supports 4 different database systems: MySql, SQL Server, Microsoft Access and SQLite. For this tutorial, I will be using SQL Server, however, you can easily follow along using any of the other 3 database servers. From a user standpoint, LayerGen was created intentially so that no code changes would have to be implemented. Regardless of the backend database, LayerGen creates compatible code in the backend that shouldn't interfere with your front-end code.

That being said, if you do plan to use MySql or SQLite, you must either download the ADO.NET connectors for your database server or you can use the ones precompiled with LayerGen 3.5. The connectors that are precompiled with LayerGen are compiled in 32-bit mode to maintain compatibility. If you use them, you must remember to set your project to x86 mode. If you want to use 64-bit, you must download the connectors from the appropriate website. The MySql connector can be downloaded from [here](https://dev.mysql.com/downloads/connector/net/6.9.html). If you want to use SQLite, you can download the connector from [here](https://system.data.sqlite.org/index.html/doc/trunk/www/downloads.wiki). If you are using Microsoft SQL Server, you do not need to worry about anything because there is already an ADO.Net connector built into the .NET framework.

The other thing you'll probably want is a database developer management tool for creating databases, tables, views and anything else imaginable. There are several tools available, but I will link the recommended tools here. All of these tools are free of charge. For SQL Server, I recommend SQL Server Management Studio. You can download it from [here](https://msdn.microsoft.com/en-us/sqlserver2014express.aspx). For MySql, I recommend MySql Workbench. It is free to use and there are versions for both Windows and Linux. You can download it from  [here](https://dev.mysql.com/downloads/workbench/). Finally, for SQLite, I recommend SQLite Expert Personal. It can be downloaded from  [here](http://www.sqliteexpert.com/download.html). Keep in mind, you are free to use any tool you wish. These are only recommendations.

## Designing the database
We are going to create a simple sales database for Foo Enterprises. To begin, create a database called **FooEnterprises**. Create a table called **Product** with the following schema:

Field Name | Data Type | Notes
-------- | -----|-
ProductId| int|Primary Key, Identity (auto increment)
ProductName| nvarchar(500)|
WholesaleCost| money|
SaleCost| money|

Insert the following rows into the new Product table:

ProductName | WholesaleCost | SaleCost
-------- | -----|-
Apples| 0.25|0.39
Oranges| 1.25|1.50
Chocolate Bar| 1.89|2.15

Next, create a table for customers and call it **Customer**:
Field Name | Data Type | Notes
-------- | -----|-
CustomerId| int|Primary Key, Identity (auto increment)
FirstName| nvarchar(100)|
LastName| nvarchar(100)|

Insert the follow rows into the new customer table:

FirstName | LastName
--------|--------
Mike|Smith
Sara|Baker

Finally, lets create one last table, called **CustomerProduct**:
Field Name | Data Type | Notes
-------- | -----|-
CustomerProductId| int|Primary Key, Identity (auto increment)
CustomerId| int|Foreign Key to Customer Table
ProductId| int|Foreign Key to Product Table
Be sure to set up a foreign key relationship between *CustomerId* in **CustomerProduct** and *CustomerId* in **Customer**. Also setup a foreign key relationship between *ProductId* in **CustomerProduct** and *ProductId* in **Product**. Finally, insert these rows into the new **CustomerProduct** table:
CustomerId | ProductId
--------|--------
1|3
1|1
2|3
2|2
2|3

## Using LayerGen 3.5
We are finally ready to use LayerGen. We are going to use LayerGen to create all the necessary code automatically for accessing our tables and data. The first time you fire up LayerGen, it will look like this:

![LayerGen Screenshot](https://www.codeproject.com/KB/database/1000660/InitialScreen1.png)

The screen is pretty intuitive and there are help icons next to every option. You can hover over any of the help icons to get a tooltip fly out with more information.

The first thing to do is select your database engine from the dropdown marked "SQL Server:". You will get the choice of "SQL Server", "MySql" or "SQLite". Depending on which one you choose, the options will change.

Fill in the appropriate options to connect to your database. Depending on the options you select, the connection string builder will automatically update as you fill in options. You can select "Custom Connection String" if you want to manually edit your connection string.

Once your connection string is built, you can click the button next to "Objects". This will bring up a list of all your tables or views in the database. LayerGen will only create code for the objects you select in this window. Since our example has no views, you will only see our 3 tables we created listed under the "Tables" section of the window. Place a check mark in all 3 check boxes, then click OK. The window will go away and you will see the selected objects in the "Objects" text box.

Next, we need to specify an output folder. Click the button next to the "Output" textbox to bring up a folder browser. Choose an empty folder on your hard drive where LayerGen can create the necessary files.

Next, in the language dropdown, select a language, either C# or VB. For this example, I will be using C#, however, you can choose VB if you wish. The generated code for both C# and VB work identical. You can also choose to include some comments in the generated code by checking the "Include Comments" checkbox. Clear this checkbox if you do not want comments (XML Documentation comments will ALWAYS be generated, regardless of this option)

Finally, in the language options section, there are various options you can set that affect the generated code. By default, LayerGen will create the code in the "DataLayer.<Database Name>" and the "BusinessLayer.<Database Name>" namespaces. If you wish to provide custom namespace names, simply check the box and type in your own namespace name.

LayerGen has the ability to retreive data based on a SQL Statement or a stored procedure and place that data in a custom class. To enable this feature, check the box thats marked "Enable Dynamic Data Retrieval". For this example, make sure this box is checked so I can demonstrate its use later. Note that enabling this feature makes the generated code only work with .NET 4.0 or higher.

The next option is the option to automatically right trim incoming strings. In Sql Server, if you create a field that has a datatype of **char(10)** or **nchar(10)**, Sql Server will add additional whitespace at the end of your string to make the length exactly 10 characters long. By enabling this option, LayerGen will automatically right trim all strings that get read from the database. Note that this only trims data being read from the database. It will still store the data as is when doing an insert or update.

The final option is an option to enable serialization. LayerGen can serialize and deserialize data from JSON, BSON and Xml formats. Enable this option for this tutorial by placing a checkmark in the box. Serialization will require a reference to Newtonsoft's JSON.Net library. It can either be obtained through  [NuGet](https://www.nuget.org/packages/Newtonsoft.Json/)  or through  [Newtonsoft's website](http://www.newtonsoft.com/json).

Once you set all your options, click the Create Layers button and LayerGen will do its thing. Here is a screen shot of my LayerGen settings:

![LayerGen Screenshot](https://www.codeproject.com/KB/database/1000660/SettingsScreen1.png)


Once LayerGen is finished, you can close out of it and browse to the output folder you specified on your hard drive. For each table or view selected, LayerGen will create two files, a data layer file and a business layer file. In addition, it also creates a file called "Universal". This file contains the connection string as well as some shared stuff, like exceptions. Also generated is a "StoredProcedures.Sql" file. This file contains the necessary stored procedures for basic CRUD operations required to make LayerGen work with stored procedures. If you are not using stored procedures, then you won't need this file. Note that this file will not be created if you are using SQLite because SQLite currently doesn't support stored procedures.

The next step is to fire up Visual Studio. For this example, I am using Visual Studio 2015. Create a new console application called "Foo" and make sure it targets .NET 4.0 or higher. Once the project has been created, right click on the solution and add a new class library project. You can call this project "FooLayers". Even though its not required, its always a good idea to keep your data layers seperated from your main UI. This way you can reuse the layers in another application, if you need to.

Delete the "Class1.cs" file that visual studio automatically makes and then right click on the "FooLayers" project and click "Manage NuGet Packages". Search for "Json.Net" and add that to the project. Alternatively, you can right click on the "FooLayers" project and click "Add Reference..." and manually browse to the Json.Net DLL file. Next, click "Add Existing Item". Add all the files LayerGen created for you, except the StoredProceduresScript.Sql file. Finally, right click on references on the Foo project and add a reference to the FooLayers project. Build the project and you should have no errors!

### Using LayerGen's generated code
Finally, its time to get our hands dirty with some code. For the first example, I want to show you how easy it is to get all the rows in a table and just print them to the console. Let's print out all of our products. Add this code to the Main method in Program.cs in the Foo project:

    var products = new BusinessLayer.FooEnterprises.Products();
    products.GetAll();
    
    foreach (BusinessLayer.FooEnterprises.Product product in products)
    {
        Console.WriteLine("{0}", product.ProductName);
    }

Run the program and you should see a list of all our products we added earlier printed to the screen. When LayerGen creates your code, it creates several classes. The main class name is based off whatever the table name is. In addition, it creates a collection class for the main class by appending an "s" to the end of the main class name (called pluralizing). One of the methods in this collection class is a method called "GetAll", which pulls all rows from the table.

Suppose we wanted only products that contain the letter "L". Remove the code you just created and replace it with this code:

    var lProducts = new BusinessLayer.FooEnterprises.Products();
    lProducts.GetBySqlStatement(
        "SELECT * FROM " + BusinessLayer.FooEnterprises.Product.LgTableName + " WHERE ProductName LIKE '{0}'", "%L%");
    
    foreach (BusinessLayer.FooEnterprises.Product product in lProducts)
    {
        Console.WriteLine("{0}", product.ProductName);
    }

Running the program now will print "Apples" and "Chocolate Bar", the only 2 products we have that contain the letter "L". In addition to the GetAll() method, the collection class also contains a GetBySqlStatement() method. This method retrieves data based on a SQL statement passed into it. You'll notice that in the string, we used {0} instead of the actual parameter. We do this for two reasons. First, to avoid Sql Injection and second, to escape our strings. Sql Injection is a type of hack that allows hackers to dynamically change your Sql and possibly get sensitive data or even delete data. ALWAYS use the {0} style to pass in parameters! Another thing to note in the example above is the use of "BusinessLayer.FooEnterprises.Product.LgTableName". This is a constant that returns a string containing the name of the table. There is also a property called LgPrimaryKey which returns the name of the primary key field.

In addition to the GetBySqlStatement() method, there is also a sister method called GetByStoredProcedure(). As the name implies, this method executes a stored procedure. You can optionally pass in a dictionary of parameters. Here is an example of the usage:

    lProducts.GetByStoredProcedure("spGetProduct", new Dictionary<string, object> {{"@id", 1}});

Of course, running this would result in a runtime error because our simple database doesn't have that stored procedure. However, if you want to create a stored procedure called "spGetProduct" that takes a parameter called "id", you can try it out for yourself.

### Dealing with single rows
So far, we've been using the pluralized collection class. Suppose we want to load a single row of data based on a primary key? Erase all the code you have under the Main() method and type in this code:

    var sara = new BusinessLayer.FooEnterprises.Customer(2);
    
    Console.WriteLine("{0} {1}", sara.FirstName, sara.LastName);


This will load the customer that has a CustomerId of 2, which in our simple database, is Sara. Notice we are using the singular version to load a single row (e.g. "Customer" instead of "Customers"). Whenever you pass a primary key value into the constructor, the object is automatically created and the record that matches that primary key is automatically loaded into the object.

Suppose Sara gets married and changes her last name to Jones? Updating a row is very simple. Remove the Console.WriteLine line out and add this:

    sara.LastName = "Jones";
    sara.Save();
If you run the program again, there will be no output. However, if you pull up SQL Server Management Studio and look in the Customer table, you will see that Sara's last name did indeed change to "Jones".

Inserting a new row is just as easy. Let insert another customer named Mindy Franklin. Remove all the code from the Main() method and type in this code:

    var mindy = new BusinessLayer.FooEnterprises.Customer();
    
    mindy.FirstName = "Mindy";
    mindy.LastName = "Franklin";
    mindy.Save();
    
    Console.WriteLine("{0}'s id is {1}", mindy.FirstName, mindy.CustomerId);
Run this program and again and you'll see Mindy's new id, which should be 3. If you open SQL Server Management Studio again and look in the Customer table, you will now see that Mindy Franklin is in there. Notice we passed an empty constructor this time. If no primary key value is passed in the constructor, then the object is created blank. Also notice, since our primary key was an identity (auto increment), we did not have to specify a CustomerId. And in fact, if we tried to implicitly set a value for CustomerId, it would cause a compile time error. Also notice that once a new row is saved, the primary key value is captured (if its identity) and is stored in the CustomerId property for us.

Another thing to demonstrate is null values. Let add a new customer named Sam. And let's pretend Sam didn't want to share his last name. Remove all the lines from the Main() method once again and type in these lines:

    var sam = new BusinessLayer.FooEnterprises.Customer();
    
    sam.FirstName = "Sam";
    sam.SetNull(BusinessLayer.FooEnterprises.Customer.Fields.LastName);
    sam.Save();
    
    if (sam.IsNull(BusinessLayer.FooEnterprises.Customer.Fields.LastName))
    {
        Console.WriteLine("{0} has no last name!", sam.FirstName);
    }
    else
    {
        Console.WriteLine("{0} has a last name! It's {1}", sam.FirstName, sam.LastName);
    }
Run this program and you will see the output saying that Sam has no last name. If you look in Sql Server Management Studio, and you look in the customer table again, you will see that there is indeed a null value in Sam's LastName field. You can use the SetNull() method to set any field to a null value, as long as the field in the database allows nulls. You can also use the IsNull() method to test if a field is set to null.

One last thing to demonstrate in this section is deleting rows. Lets delete Sam from our Customer table. Remove all the lines in the Main() method and type in this code:

    var sam = new BusinessLayer.FooEnterprises.Customer(4); // Sam should be CustomerId 4
    sam.Delete();
Before running this, you should double check inside Sql Server Management Studio and make sure Sam's CustomerId is 4. Running this program will product no output. However, if run this program, then run Sql Server Management Studio and look in the Customer table, you'll see Sam is no longer in the database. Bye bye Sam!

### Foreign Keys
Whenever LayerGen comes across a foreign key in a table, it creates a special property prefaced with "F" and ending with whatever the Id field name of the foreign key. This property can be used to access the primary key table directly without having to instantiate a new instance of the primary key table. In addition, you get new "GetBy" methods that allow you to get data based on an id of the primary tables. Examples always speak louder than words. Remove all the lines in the Main() method again and type in this code:

    var purchases = new BusinessLayer.FooEnterprises.CustomerProducts();
    purchases.GetByCustomerId(1);
    
    foreach (BusinessLayer.FooEnterprises.CustomerProduct purchase in purchases)
    {
        Console.WriteLine("{0} {1} bought a(n) {2}", purchase.FCustomerId.FirstName,
            purchase.FCustomerId.LastName, purchase.FProductId.ProductName);
    }
If you remember the table we created called CustomerProduct, it contained no data. It only contained IDs linking customers and products. If you run this program, it will show all the purchases Mike Smith made (a chocolate bar and an apple). Notice that the CustomerProducts() collection contains a method called GetByCustomerId() which pulls all data in the CustomerProducts table where the customer id is equal to 1 (Mike Smith's customer ID). There is also a similar method called GetByProductId() which you could use to see all the customers who bought a specific product. Notice also, we didn't have to do this:

    foreach (BusinessLayer.FooEnterprises.CustomerProduct purchase in purchases)
    {
        var customer = new BusinessLayer.FooEnterprises.Customer(purchase.CustomerId);
        var product = new BusinessLayer.FooEnterprises.Product(purchase.ProductId);
    
        Console.WriteLine("{0} {1} bought a(n) {2}", customer.FirstName, customer.LastName, product.ProductName);
    }
Even though this would produce the same results, we didn't have to do this because of our special "F" properties. It's also worth mentioning that data is not retrieved from the database until our special "F" properties are used for the first time. This saves on bogging down the server by unnecessarily loading data you may or may not ever use.
### Stored Procedures
f you are using Microsoft Sql Server or MySql as your database backend, LayerGen completely supports the use of stored procedures for CRUD operations instead of the default Sql Text operations. To tell LayerGen to use stored procedures instead of Sql text, you simple pass in a value of "true" into the constructor. Here is an example:

    var mike = new BusinessLayer.FooEnterprises.Customer(1, true);
Any CRUD operation (Inserting, Deleting, Updating or Retrieving) you perform on the mike object will now be done using stored procedures instead of Sql text. If you want to try this out, you must first open the StoredProcedureScripts.Sql file that LayerGen created for you and you must execute that Sql file inside Sql Server Management Studio or MySql Workbench. This script creates the necessary procedures for supporting the basic CRUD operations. Of course, you are free to create your own stored procedures and calling the procedures using the GetByStoredProcedure() method, as explained above. Also note that the pluralized collection class also has the same overload for enabling stored procedures:

    var customers = new BusinessLayer.FooEnterprises.Customers(true);
    customers.GetAll();
### Partial Loading
Sometimes it might be necessary to load a row, but only load some of the fields instead of all of the fields. This is especially true when you are storing images or binary data inside a row. Because images or binary data might be large, you may only want to load all the fields except those high volume fields. LayerGen allows you to do this. Remove all the lines from our Main() method once again and type in these lines:

    var mike = new BusinessLayer.FooEnterprises.Customer(1,
        new List<BusinessLayer.FooEnterprises.Customer.Fields>
        {
            BusinessLayer.FooEnterprises.Customer.Fields.FirstName
        });
What this program will do is load Mike's record, but ONLY his first name. Any field that is not loaded will get set to null. Because of the fact that unloaded fields get set to null, calling the Save() method will throw an exception of type "BusinessLayer.FooEnterprises.ReadOnlyException". There is an overload on the Save() method that allows you to force save the record. Calling Save(true) will force the row to save anyway. However, doing this will overwrite the unloaded fields with null values. Use that with extreme caution. You have been warned!
### Concurrency Options
If you are using LayerGen in an application where several people might be editing the same rows all at once, you'll want to enable LayerGen's concurrency checking.

To understand concurrency checking, imagine this scenario. Suppose Bill is updating a customer's information. While Bill is typing in the new information, imagine if Bill's co-worker, Joe, is also updating something on the same customer. Joe updates the customer information, then saves the information to the database. Now, 45 seconds later, Bill is done updating the information on the same customer and he saves his updates to the same customer Joe just previously updated. Joe's information will now be overriden with Bill's changes. Bad!

Fortunately, LayerGen has concurrency protection built in, however, by default it is turned off. The reason its off by default is because concurrency checking takes a little extra time and memory when saving and loading rows. Concurrency checking should only be turned on if you actually are writing an application in which several people can change the same rows at once.

Enabling concurrency checking is very easy to do. Poor Sara has gone through a divorce and its time to change her name back to Baker. Remove the lines from the Main() method again and type in this program, but do NOT run it yet:

    var sara = new BusinessLayer.FooEnterprises.Customer(2,
        BusinessLayer.FooEnterprises.Customer.ConcurrencyOptions.Strict);
    
    sara.LastName = "Baker";
    sara.Save();
Before running this program, set a breakpoint on the line "sara.Save();". When you run the program, make sure you run it in debug mode so that Visual Studio will stop at the breakpoint before saving the row. If you examine the "sara" object in Visual Studio, you'll notice that when Sara's record was loaded, her last name was set to Jones. Now switch over to Sql Server Management Studio and edit Sara's record and change her LastName or FirstName to something else. Doesn't matter what. Now switch back to Visual Studio and hit <F5> to resume the program. Notice an exception of type BusinessLayer.FooEnterprises.OutOfSyncException was thrown. This exception is thrown because the data in that record is different now from when the record was first loaded. In your application, you'd always want to catch that exception so that you can alert the user or something. The pluralized collection classes also have this overloaded constructor to enable concurrency checking.
### Serialization/Deserialization
Data in LayerGen can be serialized to various formats. Let's say we wanted to serialze Sara's row to JSON. Remove the lines from the Main() method again and type in this program:

    var sara = new BusinessLayer.FooEnterprises.Customer(2);
    string saraJson = sara.ToString(BusinessLayer.FooEnterprises.SerializationFormats.Json);
    
    Console.WriteLine(saraJson);
Run this and you'll see Sara's record printed to the screen in JSON format. If you wish, you can change the program to this:

    var sara = new BusinessLayer.FooEnterprises.Customer(2);
    string saraXml = sara.ToString(BusinessLayer.FooEnterprises.SerializationFormats.Xml);
    
    Console.WriteLine(saraXml);
Running this will output Sara's record to the screen in Xml format instead of JSON format.

You may notice an additional field called "SerializationIsUpdate". This field is used internally by LayerGen to do deserialization and can be ignored. Make sure to include it though if you plan on deserializing the data back.

In addition to serializing a single row, LayerGen can also serialize multiple rows. Let's say we wanted to serialize every customer in our customer table to JSON:

    var customers = new BusinessLayer.FooEnterprises.Customers();
    customers.GetAll();
    
    string customersJson = customers.ToString(BusinessLayer.FooEnterprises.SerializationFormats.Json);
    
    Console.WriteLine(customersJson);
Run this and you will see all of our customers printed to the screen in JSON format. If you wish, you can change the code to output in Xml format instead of JSON, like we did with the above program.

Deserializing from JSON, Xml or BSON is just as easy. Try this program out:

    var customers = new BusinessLayer.FooEnterprises.Customers();
    customers.GetAll();
    
    string customersJson = customers.ToString(BusinessLayer.FooEnterprises.SerializationFormats.Json);
    
    BusinessLayer.FooEnterprises.Customers reconstructedCustomers =
        BusinessLayer.FooEnterprises.Customers.FromJson(customersJson);
    
    foreach (BusinessLayer.FooEnterprises.Customer customer in reconstructedCustomers)
    {
        Console.WriteLine("{0} {1}", customer.FirstName, customer.LastName);
    }
This program will retreive all customers, convert them into JSON, then reconstruct the original object from the JSON string. Finally, it will iterate through each customer and print their name to the screen. Again, you can deserialize not just from JSON, but also from BSON or Xml. Feel free to try it out!
### Dynamic Data Retrieval
One of the coolest features of LayerGen is the ability to dynamically retrieve data and store it into a custom class. This will become clearer with an example. Suppose we want to calculate a profit margin in our sales program. Here is the SQL we want to execute:

    SELECT  SUM([SaleCost]) AS TotalSales ,
            SUM([WholesaleCost]) AS TotalWholesaleCost ,
            1 - SUM([WholesaleCost]) / SUM([SaleCost]) AS ProfitMargin
    FROM    [CustomerProduct]
            INNER JOIN [Product] ON [Product].[ProductId] = [CustomerProduct].[ProductId];
The problem is, how do we get the result of that SQL statement? There are several ways to approach this. Option #1 is we could create a view and put that SQL statement in the view, then run LayerGen again against that view. Views are nice, but it seems like a lot of extra overhead if we are only using this statement in one place. Option #2 is we could just load all the rows from CustomerProducts and call the GetAll() method and just calculate it in the application instead of on the SQL server side. This option might work for a few rows, but suppose you had millions of rows. That would be a whole lot of data transferring and on top of that, imagine if 5 people at once were executing this. No Way! Option #3 is to do something hacky, like casting TotalSales to a VarChar and saying "As FirstName", then just call GetBySqlStatement() on the Customer table and have the results stored in FirstName and LastName. This is a really bad idea for so many reasons. Don't ever do this or I promise your co-workers will want to beat you in the head with a hammer.

Option #4 is to use LayerGen's Dynamic Data Retrieval. Dynamic Data Retrieval requires .NET 4.0 and up. I will show you an example of how this works. First, we need to add a new class called Sales. So right click on "Foo" and add a new class. The class should look like this:

    public class Sales
    {
        public decimal TotalSales { get; set; }
        public decimal TotalWholesaleCost { get; set; }
        public decimal ProfitMargin { get; set; }
    }
Next, go back to your Main() method and remove all the lines in that method and type this:

    string sql = "SELECT  SUM([SaleCost]) AS TotalSales ,";
    sql = sql + "         SUM([WholesaleCost]) AS TotalWholesaleCost ,";
    sql = sql + "         1 - SUM([WholesaleCost]) / SUM([SaleCost]) AS ProfitMargin";
    sql = sql + "         FROM    [CustomerProduct]";
    sql = sql + "         INNER JOIN [Product] ON [Product].[ProductId] = [CustomerProduct].[ProductId];";
    
    List<Sales> sales = BusinessLayer.FooEnterprises.LoadData.FromSqlStatement<Sales>(sql);
    
    if (sales.Count == 0)
    {
        Console.WriteLine("No Data!");
    }
    else
    {
        Console.WriteLine("Total Sales   ==> {0}", sales[0].TotalSales.ToString("C"));
        Console.WriteLine("Total Costs   ==> {0}", sales[0].TotalWholesaleCost.ToString("C"));
        Console.WriteLine("Profit Margin ==> {0}", sales[0].ProfitMargin.ToString("p2"));
    }
In a nutshell, what LayerGen is doing is its trying to match up the properties in the Sales class with field names returned from the Sql Statement. It then takes all the rows returned and puts them in a list. Since our Sql query was an aggregate, there will only be one row returned. LoadData also has a "FromStoredProcedure" method, which works the same, except the data comes from a stored procedure instead of Sql text. In addition to LoadData, there is also ExecuteNonQuery, for executing stored procedures or Sql statements that do not return rows (such as inserts or updates).
### Command Line Usage
LayerGen can be used from the command line. This is especially useful for integrating LayerGen into your automated build script. Here are some examples:

> Microsoft SQL Server:

     LayerGen35 "include-comments=True" "pluralization-template={ObjectName}s" "enable-dynamic-data-retrieval=True" "automatically-right-trim-data=False" "allow-serialization=True" "language=CSharp" "output=C:\LayerGen\Output" "sql-plugin=SqlServer" "sql-server-server-name=mysqlserver" "sql-server-port=1433" "sql-server-username=sa" "sql-server-default-schema=dbo" "sql-server-password=password" "sql-server-trusted-connection=False" "sql-server-database-name="MyDatabase" "data-namespace-name=DataLayer" "business-namespace-name=BusinessLayer"

MySql:

     LayerGen35 "include-comments=True" "pluralization-template={ObjectName}s" "enable-dynamic-data-retrieval=True" "automatically-right-trim-data=False" "allow-serialization=True" "language=VbNet" "output=C:\LayerGen\Output" "sql-plugin=MySql" "mysql-server-server-name=mysql" "mysql-server-port=3306" "mysql-server-username=alan" "mysql-server-password=dragonslayer" "mysql-server-database-name="MyDatabase" "data-namespace-name=DataLayer" "business-namespace-name=BusinessLayer" 

Sqlite:

     LayerGen35 "include-comments=True" "pluralization-template={ObjectName}s" "enable-dynamic-data-retrieval=True" "automatically-right-trim-data=False" "allow-serialization=True" "language=CSharp" "output=C:\LayerGen\Output" "sql-plugin=Sqlite" "sqlite-filename=C:\My Databases\Test.sqlite" "data-namespace-name=DataLayer.LayerGenTestDb" "business-namespace-name=BusinessLayer.LayerGenTestDb" 

MS Access:

     LayerGen35 "include-comments=True" "pluralization-template={ObjectName}s" "enable-dynamic-data-retrieval=True" "automatically-right-trim-data=False" "allow-serialization=True" "language=CSharp" "output=C:\LayerGen\Output" "sql-plugin=MsAccess" "msaccess-filename=C:\My Databases\Test.accdb" "data-namespace-name=DataLayer.LayerGenTestDb" "business-namespace-name=BusinessLayer.LayerGenTestDb"
Every option that's available in the GUI should be available from the command line!
### Summary

I think I touched on every feature LayerGen has to offer. This concludes the tutorial for LayerGen. I appreciate all comments and bug reports.

## LayerGen Known Bugs & Limitations
SQLite and Microsoft Access do not support stored procedures.

In SQLite, if you create a view and that view uses an aggregate function, then you must use the "TYPES" keyword before the SELECT statement. This is a bug in SQLite's ADO.Net connector and you can find details about this  [here](http://system.data.sqlite.org/index.html/artifact/c87341d109c60a64).

All tables MUST have a primary key. If there is no primary key, then LayerGen will silently skip over that table. If you run LayerGen against a table and see no output being produced, then this is most likely the culprit.

LayerGen does not support multiple primary keys (aka Composite primary keys).

LayerGen does not support multivalued fields in Microsoft Access.

