namespace CapstoneProjectAPI.Exceptions
{
    public class EntityNotFoundException : Exception
    {
        string message;
        public EntityNotFoundException()
        {
            message = "Unable to create entity";
        }
        public EntityNotFoundException(string msg)
        {
            message = msg;
        }
        public override string Message => message;
    }

}