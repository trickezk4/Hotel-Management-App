using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HotelApp.Helpers;

public static class ICommandExtensions
{
    /// <summary>
    /// Executes the ICommand and if the concrete Execute method returns a Task (custom implementation),
    /// awaits it. Works safely with standard ICommand implementations that return void.
    /// </summary>
    public static async Task ExecuteAsyncSafe(this ICommand? command, object? parameter = null)
    {
        if (command is null) return;
        if (!command.CanExecute(parameter)) return;

        // Try to invoke the concrete Execute method via reflection so we can capture a returned Task if present.
        try
        {
            var type = command.GetType();
            // Find an Execute method that accepts one parameter (object)
            var method = type.GetMethod("Execute", new Type[] { typeof(object) }) 
                         ?? type.GetMethod("Execute", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (method != null)
            {
                var result = method.Invoke(command, new object?[] { parameter });
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                    return;
                }

                // If the method returned void (most ICommand implementations), nothing to await.
                return;
            }
        }
        catch (TargetInvocationException)
        {
            // If the invoked method threw, rethrow inner for clarity
            throw;
        }

        // Fallback: call ICommand.Execute (will be void)
        command.Execute(parameter);
    }
}