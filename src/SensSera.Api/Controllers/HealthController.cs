using Microsoft.AspNetCore.Mvc;                                                                                                                        
                                                                  
  namespace SensSera.Api.Controllers;                        
                      
  [ApiController]                                
  [Route("api/[controller]")]
  public class HealthController : ControllerBase
  {
      [HttpGet]
      public IActionResult Get() => Ok(new { status = "healthy" });
  }