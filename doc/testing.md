# Testing

To be able to run the tests:

- Create an environment variable named `SqlServerTransportConnectionString` with the following connectionstring:
  `Server=.\sqlexpress;database=nservicebus;Integrated Security=true;`
- Execute the T-SQL script `Create-Databases.sql` on your `.\sqlexpress` instance, located in the `Solution items` folder.