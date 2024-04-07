using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Math;
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class CreateTransaction : MonoBehaviour
{
    private const string CreateTransactionMethod = "CreateTransaction";
    private const string GetBalanceMethod = "GetBalance";

    [Header("Payload")]
    [SerializeField] private int version;
    [SerializeField] private long nonce;
    [SerializeField] private string toAddress;
    [SerializeField] private string amount;
    [SerializeField] private string gasPrice;
    [SerializeField] private string gasLimit;
    [SerializeField] private string code;
    [SerializeField] private string data;
    [SerializeField] private bool priority;

    [Header("KeyPair")]
    [SerializeField] private string publicKey;
    [SerializeField] private string privateKey;

    [Space(15)]
    [SerializeField] private bool autoNonce;        // enable to auto increase nonce
    [SerializeField] private string walletAddress;

    private ECKeyPair ecKeyPair;

    private void Awake()
    {
        ecKeyPair = new ECKeyPair(new BigInteger(publicKey, 16), new BigInteger(privateKey, 16));
    }

    private void Start()
    {
        StartCoroutine(Transact());
    }

    private IEnumerator Transact()
    {
        Transaction transactionParam = new Transaction()
        {
            version = this.version,
            nonce = this.nonce,
            toAddr = this.toAddress,
            amount = this.amount,
            pubKey = this.publicKey,
            gasPrice = this.gasPrice,
            gasLimit = this.gasLimit,
            code = this.code,
            data = this.data,
            priority = this.priority,
        };

        // If enabled, call GetBalance to get nonce counter
        if (autoNonce)
        {
            if (string.IsNullOrEmpty(walletAddress))
                Debug.LogError("Failed to auto increase nonce. Please write your wallet address!");
            else
            {
                ZilRequest getBalanceReq = new ZilRequest(GetBalanceMethod, walletAddress);
                yield return StartCoroutine(PostRequest(getBalanceReq, (result, error) =>
                    {
                        if (result != null)
                        {
                            Balance balance = ((JObject)result).ToObject<Balance>();
                            transactionParam.nonce = balance.nonce + 1;
                        }
                        else if (error != null)
                        {
                            Debug.Log("Error code: " + error.code + "\n" + "Message: " + error.message);
                        }
                    }
                ));
            }
        }

        // Signing process
        byte[] message = transactionParam.Encode();
        Signature signature = Schnorr.Sign(ecKeyPair, message);
        transactionParam.signature = signature.ToString().ToLower();

        ZilRequest createTxReq = new ZilRequest(CreateTransactionMethod, new object[] { transactionParam });
        StartCoroutine(PostRequest(createTxReq, (result, error) =>
            {
                if (result != null)
                {
                    Transaction.Response txResponse = ((JObject)result).ToObject<Transaction.Response>();
                    Debug.Log("Info: " + txResponse.Info + "\n" + "Tx hash: " + "0x" + txResponse.TranID);
                }
                else if (error != null)
                {
                    Debug.Log("Error code: " + error.code + "\n" + "Message: " + error.message);
                }
            }
        ));
    }


    private IEnumerator PostRequest(ZilRequest request, Action<object, ZilResponse.Error> onComplete = null)
    {
        string json = request.ToJson();
        using UnityWebRequest webRequest = new UnityWebRequest("https://dev-api.zilliqa.com/", "POST");
        byte[] rawData = Encoding.UTF8.GetBytes(json);
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.uploadHandler = new UploadHandlerRaw(rawData);
        webRequest.downloadHandler = new DownloadHandlerBuffer();

        yield return webRequest.SendWebRequest();

        switch (webRequest.result)
        {
            case UnityWebRequest.Result.Success:
                var response = JsonConvert.DeserializeObject<ZilResponse>(webRequest.downloadHandler.text);
                onComplete?.Invoke(response.result, response.error);
                break;
            default:
                break;
        }
    }
}

public class ECKeyPair
{
    public BigInteger publicKey;
    public BigInteger privateKey;

    public ECKeyPair(BigInteger publicKey, BigInteger privateKey)
    {
        this.publicKey = publicKey;
        this.privateKey = privateKey;
    }
}
