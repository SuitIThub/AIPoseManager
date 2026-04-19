using System.Collections.Generic;

namespace StudioPoseBridge.Http
{
    public sealed class ApiResponse
    {
        public bool ok { get; set; }
        public object data { get; set; }
        public string error { get; set; }
        public string code { get; set; }

        public static ApiResponse Success(object data = null)
        {
            return new ApiResponse { ok = true, data = data };
        }

        public static ApiResponse Fail(string error, string code, int httpStatus)
        {
            return new ApiResponse { ok = false, error = error, code = code, _httpStatus = httpStatus };
        }


        internal int _httpStatus { get; set; }

        public int HttpStatus
        {
            get
            {
                if (_httpStatus > 0) return _httpStatus;
                return ok ? 200 : 400;
            }
        }
    }
}
