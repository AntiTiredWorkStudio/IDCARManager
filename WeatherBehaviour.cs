using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using ICSharpCode.SharpZipLib.GZip;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System;
using System.Text;
public class WeatherObject 
{
    public WeatherData data;
}
public class WeatherData
{
    public List<WeatherForecast> forecast;
    public WeatherForecast Today
    {
        get
        {
            if (forecast.Count > 0)
            {
                return forecast[0];
            }
            return new WeatherForecast();
        }
    }
}
public struct WeatherForecast
{
    public string date;
    public string high;
    public string fengli;
    public string low;
    public string fengxiang;
    public string type;
    public override string ToString()
    {
        return date+" "+ high+" "+ fengli+" "+low+" "+fengxiang+" "+type;
    }
}
public struct CityObject
{
    public struct CityData{
        public string region;
        public string city;
    } ;
    public string code;
    public int status;
    public CityData data;
}

public struct IPObject
{
    public string cip;
    public string cid;
    public string cname;
}

public struct CityInfoObject
{
    public string id;
    public string cityZh;
}

public class WeatherBehaviour : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        StartCoroutine(RequestWeather());
    }


    public string CityCode(string cityName)
    {
        TextAsset cityCodeTable = Resources.Load<TextAsset>("citycode");
        if (cityCodeTable == null) return "101070101";
        List<CityInfoObject> citys = JsonConvert.DeserializeObject<List<CityInfoObject>>(cityCodeTable.text);
        List<string> citycodelist = new List<string>( from CityInfoObject target in citys where target.cityZh == cityName select target.id);
        if(citycodelist.Count > 0)
        {
            return citycodelist[0];
        }
        return "101070101";
    }

    public string DateTimeToStamp(DateTime now)
    {
        DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1)); // 当地时区
        long timeStamp = (long)(now - startTime).TotalMilliseconds; // 相差毫秒数
        Debug.Log("\n 当前 时间戳为：" + timeStamp);
        return timeStamp.ToString();
    }

    IEnumerator RequestWeather()
    {
        /*if (System.IO.File.Exists(Application.persistentDataPath + "/request.txt"))
        {
            System.IO.FileInfo info = new System.IO.FileInfo(Application.persistentDataPath + "/request.txt");
            int time = int.Parse(DateTimeToStamp( info.CreationTimeUtc ))- int.Parse(DateTimeToStamp(System.DateTime.Now));
            if (time < 3600 * 6)
            {
                WeatherObject Weather = JsonConvert.DeserializeObject<WeatherObject>(System.IO.File.ReadAllText(Application.persistentDataPath + "/request.txt"));
                OnWeatherInit(Weather.data.Today, city.data.city);
                
            }
        }*/
            UnityWebRequest ipRequest = UnityWebRequest.Get("http://pv.sohu.com/cityjson?ie=utf-8");
            yield return ipRequest.SendWebRequest();
            IPObject ip = JsonConvert.DeserializeObject<IPObject>(ipRequest.downloadHandler.text.Replace("var returnCitySN = ", "").Replace(";", ""));
            UnityWebRequest cityRequest = UnityWebRequest.Get("http://ip.taobao.com/service/getIpInfo.php?ip="+ ip.cip);
            yield return cityRequest.SendWebRequest();
            CityObject city = JsonConvert.DeserializeObject<CityObject>(cityRequest.downloadHandler.text);
            UnityWebRequest weatherRequest = UnityWebRequest.Get("http://wthrcdn.etouch.cn/weather_mini?citykey="+ CityCode(city.data.city));
            weatherRequest.SetRequestHeader("Content-Type", "application/octet-stream");
            weatherRequest.SetRequestHeader("Content-Encoding", "gzip");
            yield return weatherRequest.SendWebRequest();
            System.IO.File.WriteAllBytes(Application.persistentDataPath + "/request.zip", weatherRequest.downloadHandler.data);
            System.IO.FileStream inStream = new System.IO.FileStream(Application.persistentDataPath + "/request.zip", System.IO.FileMode.Open);
            System.IO.FileStream outStream = new System.IO.FileStream(Application.persistentDataPath + "/request.txt", System.IO.FileMode.Create);
            GZip.Decompress(inStream, outStream, true);
            WeatherObject Weather = JsonConvert.DeserializeObject<WeatherObject>(System.IO.File.ReadAllText(Application.persistentDataPath + "/request.txt"));
            OnWeatherInit(Weather.data.Today, city.data.city);
    }

    public virtual void OnWeatherInit(WeatherForecast forecast,string cityName)
    {
        Debug.LogWarning(cityName+":"+forecast.ToString());
    }
}
