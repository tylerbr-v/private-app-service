using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System;
using DotNetEnv;

namespace Microsoft.BotBuilderSamples
{
    [ApiController]
    [Route("api/passwordcheck2")]
    public class PasswordCheck2 : ControllerBase
    {
        public class Credential
        {
            public string password { get; set; }
        }

        public class ResponseData
        {
            public bool value { get; set; }
            public string url { get; set; }
        }

        [HttpPost]
        public IActionResult PostMethod([FromBody] Credential credentials)
        {

            if (credentials.password == "connected")
            {
                Env.Load();
                string botApiKey = Environment.GetEnvironmentVariable("BASE_ADDRESS");
                return Ok(new ResponseData { value = true, url = botApiKey });
            }
            else
            {
                return Ok(new ResponseData { value = false, url = "" });
            }
        }
    }
}
