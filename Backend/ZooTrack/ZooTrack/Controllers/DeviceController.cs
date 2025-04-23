using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooTrack.Data;
using ZooTrack.Models;

namespace ZooTrack.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class DeviceController : Controller
    {
        private readonly ZootrackDbContext _context;

        public DeviceController(ZootrackDbContext context)
        {
            _context = context;
        }

        // GET: api/Device
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Device>>> GetDevice()
        {
            return await _context.Devices.ToListAsync();
        }

        // GET: api/Device/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Device>> GetDevice(int id)
        {
            var device = await _context.Devices.FindAsync(id);

            if(device == null) 
                return NotFound();

            return device;
        }

        // POST: api/Device
        [HttpPost]
        public async Task<ActionResult<Device>> PostDevice(Device device)
        {
            _context.Devices.Add(device);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDevice), new { id = device.DeviceId }, device);
        }

        // PUT: api/Device/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDevice(int id, Device device)
        {
            if (id != device.DeviceId)
                return BadRequest();

            _context.Entry(device).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Devices.Any(e => e.DeviceId == id))
                    return NotFound();
                else
                    throw;
            }

            return NoContent();
        }

        // DELETE: api/device/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDevice(int id)
        {
            var device = await _context.Devices.FindAsync(id);
            if (device == null)
                return NotFound();

            _context.Devices.Remove(device);
            await _context.SaveChangesAsync();

            return NoContent();
        }
















        
    }
}
